using ConsoleOps.Application.Integrations.ApplicationMonitoring;
using ConsoleOps.Application.Integrations.GitHub;
using ConsoleOps.Domain.Monitoring;

namespace ConsoleOps.Application.Features.Dashboard.GetOverview;

public interface IDashboardOverviewReadStore
{
    Task<DashboardOverviewData> ReadAsync(CancellationToken cancellationToken);
}

public sealed record DashboardOverviewData(
    IReadOnlyList<DashboardSurfaceData> Surfaces,
    IReadOnlyList<DashboardActivityData> Activities);

public sealed record DashboardSurfaceData(
    Guid ProjectId,
    string ProjectName,
    string Repository,
    string DefaultBranch,
    string? WorkflowFile,
    Guid EnvironmentId,
    string EnvironmentName,
    string EnvironmentKind,
    bool HealthConfigured,
    bool VersionConfigured,
    DateTimeOffset? ConfigurationChangedAtUtc,
    DashboardSourceData? Source,
    DashboardWorkflowData? Workflow,
    DashboardHealthData? Health,
    DashboardVersionData? Version,
    DashboardVersionSyncData? VersionSync,
    IReadOnlyList<double> ResponseSamples);

public sealed record DashboardSourceData(
    string Repository,
    string DefaultBranch,
    string? CommitSha,
    string? ShortCommitSha,
    DateTimeOffset? CommittedAtUtc,
    DateTimeOffset ObservedAtUtc);

public sealed record DashboardWorkflowData(
    string? WorkflowFile,
    string? WorkflowName,
    GitHubWorkflowState State,
    string? CommitSha,
    DateTimeOffset? StartedAtUtc,
    DateTimeOffset? CompletedAtUtc,
    DateTimeOffset ObservedAtUtc);

public sealed record DashboardHealthData(
    ApplicationHealthState State,
    double? ResponseMilliseconds,
    DateTimeOffset ObservedAtUtc,
    IReadOnlyList<DashboardDependencyData> Dependencies);

public sealed record DashboardDependencyData(
    string Name,
    ApplicationHealthState State);

public sealed record DashboardVersionData(
    ApplicationVersionState State,
    string? Application,
    string? Version,
    string? CommitSha,
    string? Environment,
    DateTimeOffset? BuiltAtUtc,
    DateTimeOffset ObservedAtUtc);

public sealed record DashboardVersionSyncData(
    VersionSyncState State,
    int? CommitsBehind,
    DateTimeOffset ObservedAtUtc);

public sealed record DashboardActivityData(
    Guid Id,
    Guid ProjectId,
    string ProjectName,
    string EnvironmentName,
    MonitoringActivityType Type,
    DateTimeOffset OccurredAtUtc);
