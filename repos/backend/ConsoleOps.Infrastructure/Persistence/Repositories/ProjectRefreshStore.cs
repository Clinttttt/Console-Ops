using ConsoleOps.Application.Features.Projects.RefreshProject;
using ConsoleOps.Application.Integrations.ApplicationMonitoring;
using ConsoleOps.Domain.Monitoring;
using ConsoleOps.Domain.Projects;
using ConsoleOps.Infrastructure.Persistence.Monitoring;
using Microsoft.EntityFrameworkCore;

namespace ConsoleOps.Infrastructure.Persistence.Repositories;

internal sealed class ProjectRefreshStore(ConsoleOpsDbContext dbContext) : IProjectRefreshStore
{
    public async Task<ProjectRefreshContext?> GetContextAsync(
        Guid projectId,
        CancellationToken cancellationToken)
    {
        Project? project = await dbContext.Projects
            .AsNoTracking()
            .Include(candidate => candidate.Environments)
            .SingleOrDefaultAsync(
                candidate => candidate.Id == projectId && !candidate.IsArchived,
                cancellationToken);
        if (project is null)
        {
            return null;
        }

        List<HealthObservationEntity> latestHealth = await dbContext.HealthObservations
            .AsNoTracking()
            .Where(observation => observation.ProjectId == projectId)
            .GroupBy(observation => observation.EnvironmentId)
            .Select(group => group
                .OrderByDescending(observation => observation.ObservedAtUtc)
                .ThenByDescending(observation => observation.Id)
                .First())
            .ToListAsync(cancellationToken);
        List<VersionSyncObservationEntity> latestSync = await dbContext.VersionSyncObservations
            .AsNoTracking()
            .Where(observation => observation.ProjectId == projectId)
            .GroupBy(observation => observation.EnvironmentId)
            .Select(group => group
                .OrderByDescending(observation => observation.ObservedAtUtc)
                .ThenByDescending(observation => observation.Id)
                .First())
            .ToListAsync(cancellationToken);

        Dictionary<Guid, EnvironmentMonitoringBaseline> baselines = project.Environments
            .ToDictionary(
                environment => environment.Id,
                environment => new EnvironmentMonitoringBaseline(
                    latestHealth.FirstOrDefault(observation =>
                        observation.EnvironmentId == environment.Id)?.State,
                    latestSync.FirstOrDefault(observation =>
                        observation.EnvironmentId == environment.Id)?.State));
        ProjectRefreshEnvironment[] environments = project.Environments
            .OrderBy(environment => environment.Name, StringComparer.OrdinalIgnoreCase)
            .Select(environment => new ProjectRefreshEnvironment(
                environment.Id,
                environment.Name,
                ToCamelCase(environment.Kind),
                environment.HealthUrl,
                environment.VersionUrl))
            .ToArray();

        return new ProjectRefreshContext(
            project.Id,
            project.ConfigurationVersion,
            project.RepositoryOwner,
            project.RepositoryName,
            project.DefaultBranch,
            project.WorkflowFile,
            environments,
            baselines);
    }

    public async Task<ProjectRefreshSaveOutcome> SaveAsync(
        ProjectRefreshWriteModel refresh,
        CancellationToken cancellationToken)
    {
        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        Project? currentProject = await dbContext.Projects
            .FromSqlInterpolated($"SELECT * FROM projects WHERE id = {refresh.ProjectId} FOR UPDATE")
            .SingleOrDefaultAsync(cancellationToken);
        if (currentProject is null || currentProject.IsArchived)
        {
            await transaction.RollbackAsync(cancellationToken);
            return ProjectRefreshSaveOutcome.ProjectNotActive;
        }

        if (currentProject.ConfigurationVersion != refresh.ConfigurationVersion)
        {
            await transaction.RollbackAsync(cancellationToken);
            return ProjectRefreshSaveOutcome.ConfigurationConflict;
        }

        dbContext.SourceObservations.Add(new SourceObservationEntity
        {
            Id = Guid.CreateVersion7(),
            ProjectId = refresh.ProjectId,
            IsAvailable = refresh.Source.IsAvailable,
            Repository = refresh.Source.Repository,
            DefaultBranch = refresh.Source.DefaultBranch,
            CommitSha = refresh.Source.CommitSha,
            ShortCommitSha = refresh.Source.ShortCommitSha,
            CommittedAtUtc = refresh.Source.CommittedAtUtc,
            Failure = refresh.Source.Failure,
            ObservedAtUtc = refresh.Source.ObservedAtUtc
        });
        dbContext.WorkflowObservations.Add(new WorkflowObservationEntity
        {
            Id = Guid.CreateVersion7(),
            ProjectId = refresh.ProjectId,
            WorkflowFile = refresh.Workflow.WorkflowFile,
            WorkflowName = refresh.Workflow.WorkflowName,
            State = refresh.Workflow.State,
            CommitSha = refresh.Workflow.CommitSha,
            StartedAtUtc = refresh.Workflow.StartedAtUtc,
            CompletedAtUtc = refresh.Workflow.CompletedAtUtc,
            Failure = refresh.Workflow.Failure,
            ObservedAtUtc = refresh.Workflow.ObservedAtUtc
        });

        foreach (EnvironmentObservationWriteModel environment in refresh.Environments)
        {
            Guid healthObservationId = Guid.CreateVersion7();
            dbContext.HealthObservations.Add(new HealthObservationEntity
            {
                Id = healthObservationId,
                ProjectId = refresh.ProjectId,
                EnvironmentId = environment.EnvironmentId,
                EnvironmentName = environment.EnvironmentName,
                EnvironmentKind = environment.EnvironmentKind,
                State = environment.Health.State,
                ResponseMilliseconds = environment.Health.ResponseDuration?.TotalMilliseconds,
                ObservedAtUtc = environment.Health.ObservedAtUtc,
                Dependencies = environment.Health.Dependencies.Select(dependency =>
                    new DependencyHealthObservationEntity
                    {
                        Id = Guid.CreateVersion7(),
                        HealthObservationId = healthObservationId,
                        Name = dependency.Name,
                        State = dependency.State
                    }).ToList()
            });
            dbContext.VersionObservations.Add(new VersionObservationEntity
            {
                Id = Guid.CreateVersion7(),
                ProjectId = refresh.ProjectId,
                EnvironmentId = environment.EnvironmentId,
                EnvironmentName = environment.EnvironmentName,
                EnvironmentKind = environment.EnvironmentKind,
                State = environment.Version.State,
                Application = environment.Version.Application,
                Version = environment.Version.Version,
                CommitSha = environment.Version.CommitSha,
                Environment = environment.Version.Environment,
                BuiltAtUtc = environment.Version.BuiltAtUtc,
                ObservedAtUtc = environment.Version.ObservedAtUtc
            });
            dbContext.VersionSyncObservations.Add(new VersionSyncObservationEntity
            {
                Id = Guid.CreateVersion7(),
                ProjectId = refresh.ProjectId,
                EnvironmentId = environment.EnvironmentId,
                EnvironmentName = environment.EnvironmentName,
                EnvironmentKind = environment.EnvironmentKind,
                State = environment.VersionSync.State,
                CommitsBehind = environment.VersionSync.CommitsBehind,
                ObservedAtUtc = environment.VersionSyncObservedAtUtc
            });
        }

        dbContext.MonitoringActivities.AddRange(refresh.Activities.Select(activity =>
            new MonitoringActivityEntity
            {
                Id = Guid.CreateVersion7(),
                ProjectId = refresh.ProjectId,
                EnvironmentId = activity.EnvironmentId,
                EnvironmentName = activity.EnvironmentName,
                Type = activity.Type,
                OccurredAtUtc = activity.OccurredAtUtc
            }));

        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return ProjectRefreshSaveOutcome.Saved;
    }

    private static string ToCamelCase<TEnum>(TEnum value)
        where TEnum : struct, Enum
    {
        string text = value.ToString();
        return char.ToLowerInvariant(text[0]) + text[1..];
    }
}
