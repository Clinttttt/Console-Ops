using ConsoleOps.Application.Features.Deployments.GetHistory;
using ConsoleOps.Application.Integrations.ApplicationMonitoring;
using ConsoleOps.Domain.Monitoring;
using ConsoleOps.Domain.Projects;
using ConsoleOps.Infrastructure.Persistence.Deployments;
using ConsoleOps.Infrastructure.Persistence.Monitoring;
using Microsoft.EntityFrameworkCore;

namespace ConsoleOps.Infrastructure.Persistence.Repositories;

/// <summary>
/// Reads release history and reconciles it with what the runtime reported.
/// <para>
/// A recorded run only proves that CI built a commit. This store establishes where that commit was seen
/// running by matching it against version observations, and brackets each sighting with the health
/// observations either side of it. Both are recorded facts; neither is inferred. Where no observation
/// exists the value stays absent.
/// </para>
/// </summary>
internal sealed class DeploymentHistoryReadStore(ConsoleOpsDbContext dbContext)
    : IDeploymentHistoryReadStore
{
    /// <summary>
    /// How far before the earliest sighting health observations are loaded, so a release can report the
    /// health that preceded it. Beyond this window the value reads as unobserved rather than guessed.
    /// </summary>
    private const int HealthLookbackDays = 60;

    /// <summary>Upper bound on health rows loaded for one response, newest first.</summary>
    private const int MaximumHealthRows = 5000;

    public async Task<DeploymentHistoryData> ReadAsync(
        int limit,
        CancellationToken cancellationToken)
    {
        List<DeploymentRow> deployments = await ReadDeploymentRowsAsync(limit, cancellationToken);
        if (deployments.Count == 0)
        {
            return new DeploymentHistoryData([]);
        }

        Guid[] projectIds = deployments
            .Select(row => row.Deployment.ProjectId)
            .Distinct()
            .ToArray();
        string[] commitShas = deployments
            .Select(row => row.Deployment.CommitSha)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        Dictionary<Guid, ProjectEnvironment> environments = await dbContext.ProjectEnvironments
            .AsNoTracking()
            .Where(environment => projectIds.Contains(environment.ProjectId))
            .ToDictionaryAsync(environment => environment.Id, cancellationToken);
        List<CommitSighting> sightings = await ReadSightingsAsync(
            projectIds,
            commitShas,
            cancellationToken);
        if (sightings.Count == 0)
        {
            return new DeploymentHistoryData(
                deployments.Select(row => CreateRecord(row, [])).ToArray());
        }

        Dictionary<Guid, string?> currentCommits = await ReadCurrentCommitsAsync(
            projectIds,
            cancellationToken);
        DateTimeOffset earliestSighting = sightings.Min(sighting => sighting.FirstObservedAtUtc);
        Dictionary<Guid, List<HealthPoint>> healthByEnvironment = await ReadHealthAsync(
            projectIds,
            earliestSighting,
            cancellationToken);
        Dictionary<Guid, List<SyncPoint>> syncByEnvironment = await ReadVersionSyncAsync(
            projectIds,
            earliestSighting,
            cancellationToken);

        var sightingsByCommit = sightings
            .GroupBy(
                sighting => (sighting.ProjectId, sighting.CommitSha),
                CommitKeyComparer.Instance)
            .ToDictionary(group => group.Key, group => group.ToArray(), CommitKeyComparer.Instance);

        DeploymentRecordData[] records = deployments
            .Select(row => CreateRecord(
                row,
                BuildEnvironments(
                    row,
                    sightingsByCommit,
                    environments,
                    currentCommits,
                    healthByEnvironment,
                    syncByEnvironment)))
            .ToArray();

        return new DeploymentHistoryData(records);
    }

    private async Task<List<DeploymentRow>> ReadDeploymentRowsAsync(
        int limit,
        CancellationToken cancellationToken)
    {
        var rows = await (from deployment in dbContext.Deployments.AsNoTracking()
                          join project in dbContext.Projects.AsNoTracking()
                              on deployment.ProjectId equals project.Id
                          where !project.IsArchived
                          orderby (deployment.CompletedAtUtc ?? deployment.StartedAtUtc
                              ?? deployment.RecordedAtUtc) descending,
                              deployment.ExternalRunId descending
                          select new
                          {
                              Deployment = deployment,
                              project.Name,
                              project.RepositoryOwner,
                              project.RepositoryName
                          })
            .Take(limit)
            .ToListAsync(cancellationToken);

        return rows
            .Select(row => new DeploymentRow(
                row.Deployment,
                row.Name,
                row.RepositoryOwner,
                row.RepositoryName))
            .ToList();
    }

    /// <summary>
    /// First time each environment reported one of the commits in view. Grouped on the environment id
    /// rather than the recorded environment name so a later rename cannot split one sighting in two.
    /// </summary>
    private async Task<List<CommitSighting>> ReadSightingsAsync(
        Guid[] projectIds,
        string[] commitShas,
        CancellationToken cancellationToken)
    {
        var groups = await dbContext.VersionObservations
            .AsNoTracking()
            .Where(observation => projectIds.Contains(observation.ProjectId)
                && observation.CommitSha != null
                && commitShas.Contains(observation.CommitSha))
            .GroupBy(observation => new
            {
                observation.ProjectId,
                observation.EnvironmentId,
                observation.CommitSha
            })
            .Select(group => new
            {
                group.Key.ProjectId,
                group.Key.EnvironmentId,
                group.Key.CommitSha,
                FirstObservedAtUtc = group.Min(observation => observation.ObservedAtUtc)
            })
            .ToListAsync(cancellationToken);

        return groups
            .Select(group => new CommitSighting(
                group.ProjectId,
                group.EnvironmentId,
                group.CommitSha!,
                group.FirstObservedAtUtc))
            .ToList();
    }

    /// <summary>Commit each environment reports right now, which is what makes a release current.</summary>
    private async Task<Dictionary<Guid, string?>> ReadCurrentCommitsAsync(
        Guid[] projectIds,
        CancellationToken cancellationToken)
    {
        List<VersionObservationEntity> latest = await dbContext.VersionObservations
            .AsNoTracking()
            .Where(observation => projectIds.Contains(observation.ProjectId))
            .GroupBy(observation => observation.EnvironmentId)
            .Select(group => group
                .OrderByDescending(observation => observation.ObservedAtUtc)
                .ThenByDescending(observation => observation.Id)
                .First())
            .ToListAsync(cancellationToken);

        return latest.ToDictionary(
            observation => observation.EnvironmentId,
            observation => observation.CommitSha);
    }

    private async Task<Dictionary<Guid, List<HealthPoint>>> ReadHealthAsync(
        Guid[] projectIds,
        DateTimeOffset earliestSighting,
        CancellationToken cancellationToken)
    {
        DateTimeOffset from = earliestSighting.AddDays(-HealthLookbackDays);
        var rows = await dbContext.HealthObservations
            .AsNoTracking()
            .Where(observation => projectIds.Contains(observation.ProjectId)
                && observation.ObservedAtUtc >= from)
            .OrderByDescending(observation => observation.ObservedAtUtc)
            .Take(MaximumHealthRows)
            .Select(observation => new
            {
                observation.EnvironmentId,
                observation.State,
                observation.ObservedAtUtc
            })
            .ToListAsync(cancellationToken);

        return rows
            .Select(row => new HealthPoint(row.EnvironmentId, row.State, row.ObservedAtUtc))
            .GroupBy(point => point.EnvironmentId)
            .ToDictionary(
                group => group.Key,
                group => group.OrderBy(point => point.ObservedAtUtc).ToList());
    }

    private async Task<Dictionary<Guid, List<SyncPoint>>> ReadVersionSyncAsync(
        Guid[] projectIds,
        DateTimeOffset earliestSighting,
        CancellationToken cancellationToken)
    {
        var rows = await dbContext.VersionSyncObservations
            .AsNoTracking()
            .Where(observation => projectIds.Contains(observation.ProjectId)
                && observation.ObservedAtUtc >= earliestSighting)
            .OrderByDescending(observation => observation.ObservedAtUtc)
            .Take(MaximumHealthRows)
            .Select(observation => new
            {
                observation.EnvironmentId,
                observation.State,
                observation.ObservedAtUtc
            })
            .ToListAsync(cancellationToken);

        return rows
            .Select(row => new SyncPoint(row.EnvironmentId, row.State, row.ObservedAtUtc))
            .GroupBy(point => point.EnvironmentId)
            .ToDictionary(
                group => group.Key,
                group => group.OrderBy(point => point.ObservedAtUtc).ToList());
    }

    private static DeploymentEnvironmentData[] BuildEnvironments(
        DeploymentRow row,
        IReadOnlyDictionary<(Guid ProjectId, string CommitSha), CommitSighting[]> sightingsByCommit,
        IReadOnlyDictionary<Guid, ProjectEnvironment> environments,
        IReadOnlyDictionary<Guid, string?> currentCommits,
        IReadOnlyDictionary<Guid, List<HealthPoint>> healthByEnvironment,
        IReadOnlyDictionary<Guid, List<SyncPoint>> syncByEnvironment)
    {
        DeploymentEntity deployment = row.Deployment;
        if (!sightingsByCommit.TryGetValue(
            (deployment.ProjectId, deployment.CommitSha),
            out CommitSighting[]? sightings))
        {
            return [];
        }

        List<DeploymentEnvironmentData> results = new(sightings.Length);

        foreach (CommitSighting sighting in sightings)
        {
            // An environment that has since been removed from the project is no longer part of the
            // operator's configuration, so its past sightings are not reported as if it still existed.
            if (!environments.TryGetValue(sighting.EnvironmentId, out ProjectEnvironment? environment))
            {
                continue;
            }

            HealthPoint? before = FindHealthBefore(healthByEnvironment, sighting);
            HealthPoint? after = FindHealthAfter(healthByEnvironment, sighting);
            SyncPoint? sync = FindSyncAfter(syncByEnvironment, sighting);
            bool isCurrent = currentCommits.TryGetValue(sighting.EnvironmentId, out string? currentCommit)
                && currentCommit is not null
                && string.Equals(currentCommit, deployment.CommitSha, StringComparison.OrdinalIgnoreCase);

            results.Add(new DeploymentEnvironmentData(
                environment.Id,
                environment.Name,
                ToCamelCase(environment.Kind),
                isCurrent,
                sighting.FirstObservedAtUtc,
                before?.State,
                before?.ObservedAtUtc,
                after?.State,
                after?.ObservedAtUtc,
                sync?.State,
                sync?.ObservedAtUtc));
        }

        return results
            .OrderBy(environment => environment.EnvironmentName, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static HealthPoint? FindHealthBefore(
        IReadOnlyDictionary<Guid, List<HealthPoint>> healthByEnvironment,
        CommitSighting sighting) =>
        healthByEnvironment.TryGetValue(sighting.EnvironmentId, out List<HealthPoint>? points)
            ? points.LastOrDefault(point => point.ObservedAtUtc < sighting.FirstObservedAtUtc)
            : null;

    private static HealthPoint? FindHealthAfter(
        IReadOnlyDictionary<Guid, List<HealthPoint>> healthByEnvironment,
        CommitSighting sighting) =>
        healthByEnvironment.TryGetValue(sighting.EnvironmentId, out List<HealthPoint>? points)
            ? points.FirstOrDefault(point => point.ObservedAtUtc >= sighting.FirstObservedAtUtc)
            : null;

    private static SyncPoint? FindSyncAfter(
        IReadOnlyDictionary<Guid, List<SyncPoint>> syncByEnvironment,
        CommitSighting sighting) =>
        syncByEnvironment.TryGetValue(sighting.EnvironmentId, out List<SyncPoint>? points)
            ? points.FirstOrDefault(point => point.ObservedAtUtc >= sighting.FirstObservedAtUtc)
            : null;

    private static DeploymentRecordData CreateRecord(
        DeploymentRow row,
        IReadOnlyList<DeploymentEnvironmentData> environments) => new(
        row.Deployment.Id,
        row.Deployment.ProjectId,
        row.ProjectName,
        $"{row.RepositoryOwner}/{row.RepositoryName}",
        row.Deployment.Branch,
        row.Deployment.CommitSha,
        row.Deployment.Result,
        row.Deployment.WorkflowFile,
        row.Deployment.WorkflowName,
        row.Deployment.RunUrl,
        row.Deployment.RunNumber,
        row.Deployment.TriggeredBy,
        row.Deployment.StartedAtUtc,
        row.Deployment.CompletedAtUtc,
        row.Deployment.RecordedAtUtc,
        environments);

    private static string ToCamelCase<TEnum>(TEnum value)
        where TEnum : struct, Enum
    {
        string text = value.ToString();
        return char.ToLowerInvariant(text[0]) + text[1..];
    }

    private sealed record DeploymentRow(
        DeploymentEntity Deployment,
        string ProjectName,
        string RepositoryOwner,
        string RepositoryName);

    private sealed record CommitSighting(
        Guid ProjectId,
        Guid EnvironmentId,
        string CommitSha,
        DateTimeOffset FirstObservedAtUtc);

    private sealed record HealthPoint(
        Guid EnvironmentId,
        ApplicationHealthState State,
        DateTimeOffset ObservedAtUtc);

    private sealed record SyncPoint(
        Guid EnvironmentId,
        VersionSyncState State,
        DateTimeOffset ObservedAtUtc);

    /// <summary>Commit keys compare case-insensitively, because SHAs arrive in either case.</summary>
    private sealed class CommitKeyComparer : IEqualityComparer<(Guid ProjectId, string CommitSha)>
    {
        public static CommitKeyComparer Instance { get; } = new();

        public bool Equals(
            (Guid ProjectId, string CommitSha) left,
            (Guid ProjectId, string CommitSha) right) =>
            left.ProjectId == right.ProjectId
            && string.Equals(left.CommitSha, right.CommitSha, StringComparison.OrdinalIgnoreCase);

        public int GetHashCode((Guid ProjectId, string CommitSha) value) =>
            HashCode.Combine(
                value.ProjectId,
                StringComparer.OrdinalIgnoreCase.GetHashCode(value.CommitSha));
    }
}
