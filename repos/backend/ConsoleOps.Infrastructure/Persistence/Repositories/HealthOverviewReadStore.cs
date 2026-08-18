using ConsoleOps.Application.Features.Health.GetOverview;
using ConsoleOps.Application.Integrations.ApplicationMonitoring;
using ConsoleOps.Domain.Monitoring;
using ConsoleOps.Infrastructure.Persistence.Monitoring;
using Microsoft.EntityFrameworkCore;

namespace ConsoleOps.Infrastructure.Persistence.Repositories;

/// <summary>
/// Reads recorded health for every active environment.
/// <para>
/// One window of observations is loaded per read and everything else is derived from it in memory: the latest
/// check, the run it belongs to, and the availability figure. Deriving a run in SQL would mean either a window
/// function per environment or a query per environment, and the window is already bounded by hours and by a row
/// cap - so the honest trade is one bounded read.
/// </para>
/// <para>
/// Transitions are not derived at all. They were recorded when they happened, which is the only way a change can
/// be reported after the fact without inventing it.
/// </para>
/// </summary>
internal sealed class HealthOverviewReadStore(ConsoleOpsDbContext dbContext) : IHealthOverviewReadStore
{
    /// <summary>
    /// Upper bound on observations loaded for one read. A 24-hour window at the default interval is a few
    /// hundred rows per environment; this stops a short interval or a long window from becoming unbounded.
    /// </summary>
    internal const int MaximumObservations = 20_000;

    public async Task<HealthOverviewData> ReadAsync(
        int windowHours,
        int transitionCount,
        CancellationToken cancellationToken)
    {
        DateTimeOffset since = DateTimeOffset.UtcNow.AddHours(-Math.Abs(windowHours));

        var environments = await dbContext.ProjectEnvironments
            .AsNoTracking()
            .Join(
                dbContext.Projects.AsNoTracking().Where(project => !project.IsArchived),
                environment => environment.ProjectId,
                project => project.Id,
                (environment, project) => new
                {
                    environment.Id,
                    environment.ProjectId,
                    ProjectName = project.Name,
                    EnvironmentName = environment.Name,
                    environment.Kind,
                })
            .ToListAsync(cancellationToken);

        if (environments.Count == 0)
        {
            return new HealthOverviewData([], []);
        }

        List<ObservationRow> observations = await dbContext.HealthObservations
            .AsNoTracking()
            .Where(observation => observation.ObservedAtUtc >= since)
            .OrderByDescending(observation => observation.ObservedAtUtc)
            .ThenByDescending(observation => observation.Id)
            .Take(MaximumObservations)
            .Select(observation => new ObservationRow(
                observation.EnvironmentId,
                observation.Id,
                observation.State,
                observation.ObservedAtUtc,
                observation.ResponseMilliseconds))
            .ToListAsync(cancellationToken);

        Dictionary<Guid, List<ObservationRow>> byEnvironment = observations
            .GroupBy(observation => observation.EnvironmentId)
            .ToDictionary(group => group.Key, group => group.ToList());

        // Dependencies are only needed for the latest check of each environment, so only those are loaded.
        Guid[] latestIds = [.. byEnvironment.Values.Select(rows => rows[0].Id)];
        Dictionary<Guid, List<DependencyHealthData>> dependencies = await ReadDependenciesAsync(
            latestIds,
            cancellationToken);

        EnvironmentHealthData[] health =
        [
            .. environments.Select(environment => ToEnvironmentHealth(
                environment.ProjectId,
                environment.ProjectName,
                environment.Id,
                environment.EnvironmentName,
                environment.Kind.ToString(),
                byEnvironment.GetValueOrDefault(environment.Id) ?? [],
                dependencies,
                since))
        ];

        return new HealthOverviewData(health, await ReadTransitionsAsync(transitionCount, cancellationToken));
    }

    private async Task<Dictionary<Guid, List<DependencyHealthData>>> ReadDependenciesAsync(
        Guid[] observationIds,
        CancellationToken cancellationToken)
    {
        if (observationIds.Length == 0)
        {
            return [];
        }

        var rows = await dbContext.Set<DependencyHealthObservationEntity>()
            .AsNoTracking()
            .Where(dependency => observationIds.Contains(dependency.HealthObservationId))
            .Select(dependency => new
            {
                dependency.HealthObservationId,
                dependency.Name,
                dependency.State,
            })
            .ToListAsync(cancellationToken);

        return rows
            .GroupBy(row => row.HealthObservationId)
            .ToDictionary(
                group => group.Key,
                group => group
                    .OrderBy(row => row.Name, StringComparer.OrdinalIgnoreCase)
                    .Select(row => new DependencyHealthData(row.Name, row.State))
                    .ToList());
    }

    /// <summary>
    /// Health transitions as recorded, newest first. Only health types: version drift has its own screen.
    /// </summary>
    private async Task<List<HealthTransitionData>> ReadTransitionsAsync(
        int count,
        CancellationToken cancellationToken) =>
        await (
            from activity in dbContext.MonitoringActivities.AsNoTracking()
            join project in dbContext.Projects.AsNoTracking().Where(project => !project.IsArchived)
                on activity.ProjectId equals project.Id
            where activity.Type == MonitoringActivityType.HealthFailed
                || activity.Type == MonitoringActivityType.HealthRecovered
            orderby activity.OccurredAtUtc descending, activity.Id descending
            select new HealthTransitionData(
                activity.OccurredAtUtc,
                project.Name,
                activity.EnvironmentName,
                activity.Type))
            .Take(Math.Max(1, count))
            .ToListAsync(cancellationToken);

    /// <summary>
    /// Everything the screen states about one environment, derived from its own observations newest first.
    /// </summary>
    private static EnvironmentHealthData ToEnvironmentHealth(
        Guid projectId,
        string projectName,
        Guid environmentId,
        string environmentName,
        string environmentKind,
        List<ObservationRow> newestFirst,
        Dictionary<Guid, List<DependencyHealthData>> dependencies,
        DateTimeOffset since)
    {
        if (newestFirst.Count == 0)
        {
            // Never checked inside the window: no verdict, and none is invented.
            return new EnvironmentHealthData(
                projectId,
                projectName,
                environmentId,
                environmentName,
                environmentKind,
                null,
                null,
                null,
                0,
                null,
                null,
                0,
                null);
        }

        ObservationRow latest = newestFirst[0];
        HealthCheckData checkedNow = new(
            latest.State,
            latest.ObservedAtUtc,
            latest.ResponseMilliseconds,
            dependencies.GetValueOrDefault(latest.Id) ?? []);

        UptimeReading? uptime = Uptime.Calculate(
            [.. newestFirst.Select(row => new UptimeSample(HealthConditions.From(row.State), row.ObservedAtUtc))],
            since);

        return new EnvironmentHealthData(
            projectId,
            projectName,
            environmentId,
            environmentName,
            environmentKind,
            checkedNow,
            RunStart(newestFirst, IsHealthy),
            RunStart(newestFirst, IsFailing),
            newestFirst.TakeWhile(row => IsFailing(row.State)).Count(),
            newestFirst.FirstOrDefault(row => IsHealthy(row.State))?.ObservedAtUtc,
            uptime,
            newestFirst.Count(row => IsFailing(row.State)),
            LongestOutageSeconds(newestFirst));
    }

    /// <summary>
    /// When the current unbroken run of matching checks began, or <c>null</c> when the latest check does not
    /// match. The window bounds it: a run that started before the window reports its earliest known check.
    /// </summary>
    private static DateTimeOffset? RunStart(
        List<ObservationRow> newestFirst,
        Func<ApplicationHealthState, bool> matches)
    {
        if (!matches(newestFirst[0].State))
        {
            return null;
        }

        DateTimeOffset start = newestFirst[0].ObservedAtUtc;
        foreach (ObservationRow row in newestFirst)
        {
            if (!matches(row.State))
            {
                break;
            }

            start = row.ObservedAtUtc;
        }

        return start;
    }

    /// <summary>
    /// The longest unbroken failing run in the window, measured from its first failure to the check that ended
    /// it. <c>null</c> when nothing failed, which is not the same as zero.
    /// </summary>
    private static int? LongestOutageSeconds(List<ObservationRow> newestFirst)
    {
        List<ObservationRow> oldestFirst = [.. newestFirst.OrderBy(row => row.ObservedAtUtc)];
        double longest = 0;
        DateTimeOffset? outageStart = null;

        foreach (ObservationRow row in oldestFirst)
        {
            if (IsFailing(row.State))
            {
                outageStart ??= row.ObservedAtUtc;
                continue;
            }

            if (outageStart is { } started)
            {
                longest = Math.Max(longest, (row.ObservedAtUtc - started).TotalSeconds);
                outageStart = null;
            }
        }

        if (outageStart is { } stillFailing)
        {
            // Still failing at the newest check: measured to there rather than to now, which was not observed.
            longest = Math.Max(longest, (oldestFirst[^1].ObservedAtUtc - stillFailing).TotalSeconds);
        }

        return longest > 0 ? (int)Math.Round(longest) : null;
    }

    /// <summary>Only a health endpoint answering healthy starts a healthy run; nothing else claims it.</summary>
    private static bool IsHealthy(ApplicationHealthState state) =>
        state is ApplicationHealthState.Healthy;

    private static bool IsFailing(ApplicationHealthState state) =>
        state is ApplicationHealthState.Degraded
            or ApplicationHealthState.Unhealthy
            or ApplicationHealthState.Unreachable;

    private sealed record ObservationRow(
        Guid EnvironmentId,
        Guid Id,
        ApplicationHealthState State,
        DateTimeOffset ObservedAtUtc,
        double? ResponseMilliseconds);
}
