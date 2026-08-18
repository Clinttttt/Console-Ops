namespace ConsoleOps.Application.Features.Deployments.GetHistory;

/// <summary>
/// Transport for the Deployments screen. Enumerations cross the wire as camel-case strings and every
/// fact Console Ops could not establish is <c>null</c>. No presentation data: the UI decides wording,
/// tone, and the overall verdict.
/// </summary>
/// <param name="ObservedAt">
/// Response-composition time. Relative times and day grouping are measured against it so the screen
/// reads the same whenever it renders.
/// </param>
public sealed record DeploymentHistoryResponse(
    DateTimeOffset ObservedAt,
    IReadOnlyList<DeploymentResponse> Deployments);

/// <param name="DeployedAt">
/// Instant the release is ordered and grouped by: completion when known, otherwise start, otherwise
/// the moment Console Ops first recorded the run.
/// </param>
/// <param name="DurationSeconds">
/// Wall-clock run duration, or <c>null</c> when the provider did not report both ends.
/// </param>
/// <param name="Environments">
/// Environments observed running this commit. Empty when Console Ops never saw it running, which is not
/// proof that it failed to deploy.
/// </param>
public sealed record DeploymentResponse(
    Guid Id,
    Guid ProjectId,
    string ProjectName,
    string Provider,
    string Repository,
    string Branch,
    string CommitSha,
    string CommitShortSha,
    string Result,
    string? WorkflowFile,
    string? WorkflowName,
    string? WorkflowUrl,
    int? RunNumber,
    string? TriggeredBy,
    DateTimeOffset? StartedAt,
    DateTimeOffset? CompletedAt,
    DateTimeOffset DeployedAt,
    int? DurationSeconds,
    DateTimeOffset RecordedAt,
    IReadOnlyList<DeploymentEnvironmentResponse> Environments);

public sealed record DeploymentEnvironmentResponse(
    DeploymentEnvironmentRefResponse Environment,
    bool IsCurrent,
    DateTimeOffset FirstObservedAt,
    string HealthBefore,
    DateTimeOffset? HealthBeforeObservedAt,
    string HealthAfter,
    DateTimeOffset? HealthAfterObservedAt,
    string VersionCheck,
    DateTimeOffset? VersionCheckObservedAt);

public sealed record DeploymentEnvironmentRefResponse(Guid Id, string Name, string Kind);
