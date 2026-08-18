using ConsoleOps.Application.Integrations.ApplicationMonitoring;
using ConsoleOps.Application.Integrations.GitHub;
using ConsoleOps.Domain.Monitoring;

namespace ConsoleOps.Application.Features.Projects.RefreshProject;

public interface IProjectRefreshStore
{
    Task<ProjectRefreshContext?> GetContextAsync(
        Guid projectId,
        CancellationToken cancellationToken);

    Task<ProjectRefreshSaveOutcome> SaveAsync(
        ProjectRefreshWriteModel refresh,
        CancellationToken cancellationToken);
}

public enum ProjectRefreshSaveOutcome
{
    Saved,
    ProjectNotActive,
    ConfigurationConflict
}

public sealed record ProjectRefreshContext(
    Guid ProjectId,
    long ConfigurationVersion,
    string RepositoryOwner,
    string RepositoryName,
    string DefaultBranch,
    string? WorkflowFile,
    IReadOnlyList<ProjectRefreshEnvironment> Environments,
    IReadOnlyDictionary<Guid, EnvironmentMonitoringBaseline> Baselines);

public sealed record ProjectRefreshEnvironment(
    Guid Id,
    string Name,
    string Kind,
    string? HealthUrl,
    string? VersionUrl);

public sealed record EnvironmentMonitoringBaseline(
    ApplicationHealthState? HealthState,
    VersionSyncState? VersionSyncState);

public sealed record ProjectRefreshWriteModel(
    Guid ProjectId,
    long ConfigurationVersion,
    DateTimeOffset RefreshedAtUtc,
    SourceObservationWriteModel Source,
    WorkflowObservationWriteModel Workflow,
    IReadOnlyList<EnvironmentObservationWriteModel> Environments,
    IReadOnlyList<ActivityWriteModel> Activities,
    IReadOnlyList<DeploymentRunWriteModel> Deployments);

/// <summary>
/// One workflow run recorded as a release. Runs are re-observed on every refresh, so the store keys on
/// <paramref name="RunId"/> and updates the mutable facts instead of appending a second record.
/// </summary>
public sealed record DeploymentRunWriteModel(
    long RunId,
    int? RunNumber,
    string? WorkflowFile,
    string? WorkflowName,
    string Branch,
    string CommitSha,
    GitHubWorkflowState Result,
    DateTimeOffset? StartedAtUtc,
    DateTimeOffset? CompletedAtUtc,
    string? TriggeredBy,
    string? RunUrl,
    DateTimeOffset ObservedAtUtc);

public sealed record SourceObservationWriteModel(
    bool IsAvailable,
    string Repository,
    string DefaultBranch,
    string? CommitSha,
    string? ShortCommitSha,
    DateTimeOffset? CommittedAtUtc,
    GitHubReadFailure? Failure,
    DateTimeOffset ObservedAtUtc);

public sealed record WorkflowObservationWriteModel(
    string? WorkflowFile,
    string? WorkflowName,
    GitHubWorkflowState State,
    string? CommitSha,
    DateTimeOffset? StartedAtUtc,
    DateTimeOffset? CompletedAtUtc,
    GitHubReadFailure? Failure,
    DateTimeOffset ObservedAtUtc);

public sealed record EnvironmentObservationWriteModel(
    Guid EnvironmentId,
    string EnvironmentName,
    string EnvironmentKind,
    ApplicationHealthObservation Health,
    ApplicationVersionObservation Version,
    VersionSyncAssessment VersionSync,
    DateTimeOffset VersionSyncObservedAtUtc);

public sealed record ActivityWriteModel(
    Guid EnvironmentId,
    string EnvironmentName,
    MonitoringActivityType Type,
    DateTimeOffset OccurredAtUtc);
