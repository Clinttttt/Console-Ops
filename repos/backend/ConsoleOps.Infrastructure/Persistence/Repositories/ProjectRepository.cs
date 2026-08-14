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
                candidate => candidate.NormalizedName == project.NormalizedName,
                cancellationToken))
        {
            return ProjectRegistrationOutcome.DuplicateName;
        }

        if (await dbContext.Projects.AnyAsync(
                candidate => candidate.NormalizedRepositoryOwner == project.NormalizedRepositoryOwner
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
        catch (DbUpdateException exception) when (TryGetRegistrationOutcome(exception, out ProjectRegistrationOutcome outcome))
        {
            dbContext.ChangeTracker.Clear();
            return outcome;
        }
    }

    private static bool TryGetRegistrationOutcome(
        DbUpdateException exception,
        out ProjectRegistrationOutcome outcome)
    {
        if (exception.InnerException is PostgresException postgresException
            && postgresException.SqlState == PostgresErrorCodes.UniqueViolation)
        {
            switch (postgresException.ConstraintName)
            {
                case NameConstraint:
                    outcome = ProjectRegistrationOutcome.DuplicateName;
                    return true;
                case RepositoryConstraint:
                    outcome = ProjectRegistrationOutcome.DuplicateRepository;
                    return true;
            }
        }

        outcome = default;
        return false;
    }
}
