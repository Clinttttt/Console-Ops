using ConsoleOps.Application.Features.Projects.RefreshProject;
using ConsoleOps.Application.Integrations.ApplicationMonitoring;
using ConsoleOps.Domain.Monitoring;
using ConsoleOps.Domain.Projects;
using ConsoleOps.Infrastructure.Persistence.Deployments;
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
            .Where(observation => observation.ProjectId == projectId
                && (project.UpdatedAtUtc == null || observation.ObservedAtUtc >= project.UpdatedAtUtc))
            .GroupBy(observation => observation.EnvironmentId)
            .Select(group => group
                .OrderByDescending(observation => observation.ObservedAtUtc)
                .ThenByDescending(observation => observation.Id)
                .First())
            .ToListAsync(cancellationToken);
        List<VersionSyncObservationEntity> latestSync = await dbContext.VersionSyncObservations
            .AsNoTracking()
            .Where(observation => observation.ProjectId == projectId
                && (project.UpdatedAtUtc == null || observation.ObservedAtUtc >= project.UpdatedAtUtc))
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

        await SaveDeploymentsAsync(refresh, cancellationToken);

        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return ProjectRefreshSaveOutcome.Saved;
    }

    /// <summary>
    /// Records the workflow runs seen in this refresh.
    /// <para>
    /// Runs are re-read every refresh, so an already-known run is updated rather than appended: its
    /// outcome and completion time change while it is in flight. The first sighting is preserved in
    /// <c>recorded_at_utc</c>, which is what makes "Console Ops saw this release at" meaningful.
    /// </para>
    /// </summary>
    private async Task SaveDeploymentsAsync(
        ProjectRefreshWriteModel refresh,
        CancellationToken cancellationToken)
    {
        if (refresh.Deployments.Count == 0)
        {
            return;
        }

        long[] runIds = refresh.Deployments
            .Select(deployment => deployment.RunId)
            .Distinct()
            .ToArray();
        Dictionary<long, DeploymentEntity> existing = await dbContext.Deployments
            .Where(entity => entity.ProjectId == refresh.ProjectId
                && runIds.Contains(entity.ExternalRunId))
            .ToDictionaryAsync(entity => entity.ExternalRunId, cancellationToken);

        foreach (DeploymentRunWriteModel deployment in refresh.Deployments)
        {
            if (existing.TryGetValue(deployment.RunId, out DeploymentEntity? tracked))
            {
                tracked.RunNumber = deployment.RunNumber;
                tracked.WorkflowFile = deployment.WorkflowFile;
                tracked.WorkflowName = deployment.WorkflowName;
                tracked.Branch = deployment.Branch;
                tracked.CommitSha = deployment.CommitSha;
                tracked.Result = deployment.Result;
                tracked.StartedAtUtc = deployment.StartedAtUtc;
                tracked.CompletedAtUtc = deployment.CompletedAtUtc;
                tracked.TriggeredBy = deployment.TriggeredBy;
                tracked.RunUrl = deployment.RunUrl;
                tracked.ObservedAtUtc = deployment.ObservedAtUtc;
                continue;
            }

            dbContext.Deployments.Add(new DeploymentEntity
            {
                Id = Guid.CreateVersion7(),
                ProjectId = refresh.ProjectId,
                ExternalRunId = deployment.RunId,
                RunNumber = deployment.RunNumber,
                WorkflowFile = deployment.WorkflowFile,
                WorkflowName = deployment.WorkflowName,
                Branch = deployment.Branch,
                CommitSha = deployment.CommitSha,
                Result = deployment.Result,
                StartedAtUtc = deployment.StartedAtUtc,
                CompletedAtUtc = deployment.CompletedAtUtc,
                TriggeredBy = deployment.TriggeredBy,
                RunUrl = deployment.RunUrl,
                RecordedAtUtc = deployment.ObservedAtUtc,
                ObservedAtUtc = deployment.ObservedAtUtc
            });
        }
    }

    private static string ToCamelCase<TEnum>(TEnum value)
        where TEnum : struct, Enum
    {
        string text = value.ToString();
        return char.ToLowerInvariant(text[0]) + text[1..];
    }
}
