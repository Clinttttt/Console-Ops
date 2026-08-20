using ConsoleOps.Application.Abstractions.Persistence;
using ConsoleOps.Domain.Projects;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace ConsoleOps.Infrastructure.Persistence.Repositories;

internal sealed class ProjectRepository(ConsoleOpsDbContext dbContext) : IProjectRepository
{
    private const string NameConstraint = "ux_projects_normalized_name";
    private const string RepositoryConstraint = "ux_projects_repository";

    public async Task<ProjectRegistrationOutcome> TryAddAsync(
        Project project,
        CancellationToken cancellationToken)
    {
        if (await dbContext.Projects.AnyAsync(
                candidate => !candidate.IsArchived
                    && candidate.NormalizedName == project.NormalizedName,
                cancellationToken))
        {
            return ProjectRegistrationOutcome.DuplicateName;
        }

        if (await dbContext.Projects.AnyAsync(
                candidate => !candidate.IsArchived
                    && candidate.NormalizedRepositoryOwner == project.NormalizedRepositoryOwner
                    && candidate.NormalizedRepositoryName == project.NormalizedRepositoryName,
                cancellationToken))
        {
            return ProjectRegistrationOutcome.DuplicateRepository;
        }

        dbContext.Projects.Add(project);

        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
            return ProjectRegistrationOutcome.Added;
        }
        catch (DbUpdateException exception) when (TryGetUniqueConstraint(exception, out string? constraintName))
        {
            dbContext.ChangeTracker.Clear();
            return constraintName switch
            {
                NameConstraint => ProjectRegistrationOutcome.DuplicateName,
                RepositoryConstraint => ProjectRegistrationOutcome.DuplicateRepository,
                _ => throw new InvalidOperationException($"Unsupported unique constraint: {constraintName}.")
            };
        }
    }

    public Task<Project?> GetActiveByIdAsync(Guid projectId, CancellationToken cancellationToken) =>
        dbContext.Projects
            .Include(project => project.Environments)
            // Loaded because a risk marking is removed by taking it out of this collection, and a collection that
            // was never loaded has nothing to take out - the row would survive a decision to withdraw it.
            .Include(project => project.WorkflowRisks)
            .SingleOrDefaultAsync(
                project => project.Id == projectId && !project.IsArchived,
                cancellationToken);

    public async Task<ProjectSaveOutcome> SaveChangesAsync(
        Project project,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(project);

        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
            return ProjectSaveOutcome.Saved;
        }
        catch (DbUpdateConcurrencyException)
        {
            dbContext.ChangeTracker.Clear();
            return ProjectSaveOutcome.ConfigurationConflict;
        }
        catch (DbUpdateException exception) when (TryGetUniqueConstraint(exception, out string? constraintName))
        {
            dbContext.ChangeTracker.Clear();
            return constraintName switch
            {
                NameConstraint => ProjectSaveOutcome.DuplicateName,
                RepositoryConstraint => ProjectSaveOutcome.DuplicateRepository,
                _ => throw new InvalidOperationException($"Unsupported unique constraint: {constraintName}.")
            };
        }
    }

    private static bool TryGetUniqueConstraint(
        DbUpdateException exception,
        out string? constraintName)
    {
        if (exception.InnerException is PostgresException postgresException
            && postgresException.SqlState == PostgresErrorCodes.UniqueViolation)
        {
            if (postgresException.ConstraintName is NameConstraint or RepositoryConstraint)
            {
                constraintName = postgresException.ConstraintName;
                return true;
            }
        }

        constraintName = null;
        return false;
    }
}
