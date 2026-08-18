using ConsoleOps.Application.Features.Dashboard.GetOverview;
using ConsoleOps.Domain.Projects;
using ConsoleOps.Infrastructure.Persistence.Monitoring;
using Microsoft.EntityFrameworkCore;

namespace ConsoleOps.Infrastructure.Persistence.Repositories;

internal sealed class DashboardOverviewReadStore(ConsoleOpsDbContext dbContext)
    : IDashboardOverviewReadStore
{
    private const int ResponseSampleCount = 8;
    private const int RecentActivityCount = 20;

    /// <summary>
    /// Upper bound on health checks loaded for the availability window, so one very long-running
    /// instance with many environments cannot turn the dashboard query into a table scan.
    /// </summary>
    private const int MaximumAvailabilityRows = 20_000;

    public async Task<DashboardOverviewData> ReadAsync(
        DateTimeOffset availabilitySinceUtc,
        CancellationToken cancellationToken)
    {
        List<Project> projects = await dbContext.Projects
            .AsNoTracking()
            .Include(project => project.Environments)
            .Where(project => !project.IsArchived)
            .OrderBy(project => project.Name)
            .ToListAsync(cancellationToken);
        if (projects.Count == 0)
        {
            return new DashboardOverviewData([], [], []);
        }

        Guid[] projectIds = projects.Select(project => project.Id).ToArray();
        Guid[] environmentIds = projects
            .SelectMany(project => project.Environments)
            .Select(environment => environment.Id)
            .ToArray();

        List<SourceObservationEntity> sources = await dbContext.SourceObservations
            .AsNoTracking()
            .Where(observation => projectIds.Contains(observation.ProjectId))
            .GroupBy(observation => observation.ProjectId)
            .Select(group => group
                .OrderByDescending(observation => observation.ObservedAtUtc)
                .ThenByDescending(observation => observation.Id)
                .First())
            .ToListAsync(cancellationToken);
        List<WorkflowObservationEntity> workflows = await dbContext.WorkflowObservations
            .AsNoTracking()
            .Where(observation => projectIds.Contains(observation.ProjectId))
            .GroupBy(observation => observation.ProjectId)
            .Select(group => group
                .OrderByDescending(observation => observation.ObservedAtUtc)
                .ThenByDescending(observation => observation.Id)
                .First())
            .ToListAsync(cancellationToken);
        List<HealthObservationEntity> healthHistory = await dbContext.HealthObservations
            .FromSqlInterpolated($"""
                SELECT
                    ranked.id,
                    ranked.project_id,
                    ranked.environment_id,
                    ranked.environment_name,
                    ranked.environment_kind,
                    ranked.state,
                    ranked.response_milliseconds,
                    ranked.observed_at_utc
                FROM (
                    SELECT
                        observation.*,
                        ROW_NUMBER() OVER (
                            PARTITION BY observation.environment_id
                            ORDER BY observation.observed_at_utc DESC, observation.id DESC) AS row_number
                    FROM health_observations AS observation
                    WHERE observation.environment_id = ANY ({environmentIds})
                ) AS ranked
                WHERE ranked.row_number <= {ResponseSampleCount}
                """)
            .AsNoTracking()
            .ToListAsync(cancellationToken);
        Guid[] latestHealthIds = healthHistory
            .GroupBy(observation => observation.EnvironmentId)
            .Select(group => group
                .OrderByDescending(observation => observation.ObservedAtUtc)
                .ThenByDescending(observation => observation.Id)
                .First().Id)
            .ToArray();
        List<DependencyHealthObservationEntity> dependencies = await dbContext.DependencyHealthObservations
            .AsNoTracking()
            .Where(dependency => latestHealthIds.Contains(dependency.HealthObservationId))
            .OrderBy(dependency => dependency.Name)
            .ToListAsync(cancellationToken);
        List<VersionObservationEntity> versions = await dbContext.VersionObservations
            .AsNoTracking()
            .Where(observation => environmentIds.Contains(observation.EnvironmentId))
            .GroupBy(observation => observation.EnvironmentId)
            .Select(group => group
                .OrderByDescending(observation => observation.ObservedAtUtc)
                .ThenByDescending(observation => observation.Id)
                .First())
            .ToListAsync(cancellationToken);
        List<VersionSyncObservationEntity> versionSync = await dbContext.VersionSyncObservations
            .AsNoTracking()
            .Where(observation => environmentIds.Contains(observation.EnvironmentId))
            .GroupBy(observation => observation.EnvironmentId)
            .Select(group => group
                .OrderByDescending(observation => observation.ObservedAtUtc)
                .ThenByDescending(observation => observation.Id)
                .First())
            .ToListAsync(cancellationToken);
        List<DashboardAvailabilityData> availability = await dbContext.HealthObservations
            .AsNoTracking()
            .Where(observation => environmentIds.Contains(observation.EnvironmentId)
                && observation.ObservedAtUtc >= availabilitySinceUtc)
            .OrderByDescending(observation => observation.ObservedAtUtc)
            .Take(MaximumAvailabilityRows)
            .Select(observation => new DashboardAvailabilityData(
                observation.EnvironmentId,
                observation.State,
                observation.ObservedAtUtc))
            .ToListAsync(cancellationToken);
        List<DashboardActivityData> activities = await (
            from activity in dbContext.MonitoringActivities.AsNoTracking()
            join project in dbContext.Projects.AsNoTracking().Where(project => !project.IsArchived)
                on activity.ProjectId equals project.Id
            orderby activity.OccurredAtUtc descending, activity.Id descending
            select new DashboardActivityData(
                activity.Id,
                activity.ProjectId,
                project.Name,
                activity.EnvironmentName,
                activity.Type,
                activity.OccurredAtUtc))
            .Take(RecentActivityCount)
            .ToListAsync(cancellationToken);

        List<DashboardSurfaceData> surfaces = [];
        foreach (Project project in projects)
        {
            SourceObservationEntity? source = sources.FirstOrDefault(observation =>
                observation.ProjectId == project.Id);
            WorkflowObservationEntity? workflow = workflows.FirstOrDefault(observation =>
                observation.ProjectId == project.Id);

            foreach (ProjectEnvironment environment in project.Environments
                         .OrderBy(environment => environment.Kind)
                         .ThenBy(environment => environment.Name, StringComparer.OrdinalIgnoreCase))
            {
                HealthObservationEntity? health = healthHistory
                    .Where(observation => observation.EnvironmentId == environment.Id)
                    .OrderByDescending(observation => observation.ObservedAtUtc)
                    .ThenByDescending(observation => observation.Id)
                    .FirstOrDefault();
                VersionObservationEntity? version = versions.FirstOrDefault(observation =>
                    observation.EnvironmentId == environment.Id);
                VersionSyncObservationEntity? sync = versionSync.FirstOrDefault(observation =>
                    observation.EnvironmentId == environment.Id);
                DateTimeOffset? configurationChangedAtUtc = project.UpdatedAtUtc;
                double[] responseSamples = healthHistory
                    .Where(observation => observation.EnvironmentId == environment.Id
                        && observation.ResponseMilliseconds is not null
                        && (configurationChangedAtUtc is null
                            || observation.ObservedAtUtc >= configurationChangedAtUtc))
                    .OrderBy(observation => observation.ObservedAtUtc)
                    .ThenBy(observation => observation.Id)
                    .Select(observation => observation.ResponseMilliseconds!.Value)
                    .ToArray();

                surfaces.Add(new DashboardSurfaceData(
                    project.Id,
                    project.Name,
                    $"{project.RepositoryOwner}/{project.RepositoryName}",
                    project.DefaultBranch,
                    project.WorkflowFile,
                    environment.Id,
                    environment.Name,
                    ToCamelCase(environment.Kind),
                    !string.IsNullOrWhiteSpace(environment.HealthUrl),
                    !string.IsNullOrWhiteSpace(environment.VersionUrl),
                    configurationChangedAtUtc,
                    source is null
                        ? null
                        : new DashboardSourceData(
                            source.Repository,
                            source.DefaultBranch,
                            source.CommitSha,
                            source.ShortCommitSha,
                            source.CommittedAtUtc,
                            source.ObservedAtUtc),
                    workflow is null
                        ? null
                        : new DashboardWorkflowData(
                            workflow.WorkflowFile,
                            workflow.WorkflowName,
                            workflow.State,
                            workflow.CommitSha,
                            workflow.StartedAtUtc,
                            workflow.CompletedAtUtc,
                            workflow.ObservedAtUtc),
                    health is null
                        ? null
                        : new DashboardHealthData(
                            health.State,
                            health.ResponseMilliseconds,
                            health.ObservedAtUtc,
                            dependencies
                                .Where(dependency => dependency.HealthObservationId == health.Id)
                                .Select(dependency => new DashboardDependencyData(
                                    dependency.Name,
                                    dependency.State))
                                .ToArray()),
                    version is null
                        ? null
                        : new DashboardVersionData(
                            version.State,
                            version.Application,
                            version.Version,
                            version.CommitSha,
                            version.Environment,
                            version.BuiltAtUtc,
                            version.ObservedAtUtc),
                    sync is null
                        ? null
                        : new DashboardVersionSyncData(
                            sync.State,
                            sync.CommitsBehind,
                            sync.ObservedAtUtc),
                    responseSamples));
            }
        }

        return new DashboardOverviewData(surfaces, activities, availability);
    }

    private static string ToCamelCase<TEnum>(TEnum value)
        where TEnum : struct, Enum
    {
        string text = value.ToString();
        return char.ToLowerInvariant(text[0]) + text[1..];
    }
}
