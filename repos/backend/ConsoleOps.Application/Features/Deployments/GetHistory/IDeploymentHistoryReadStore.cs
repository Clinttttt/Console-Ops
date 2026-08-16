using ConsoleOps.Application.Integrations.ApplicationMonitoring;
using ConsoleOps.Application.Integrations.GitHub;
using ConsoleOps.Domain.Monitoring;

namespace ConsoleOps.Application.Features.Deployments.GetHistory;

public interface IDeploymentHistoryReadStore
{
    Task<DeploymentHistoryData> ReadAsync(int limit, CancellationToken cancellationToken);
}

public sealed record DeploymentHistoryData(IReadOnlyList<DeploymentRecordData> Deployments);

/// <summary>
/// One recorded release and everything Console Ops established about it.
/// </summary>
public sealed record DeploymentRecordData(
    Guid Id,
    Guid ProjectId,
    string ProjectName,
    string Repository,
    string Branch,
    string CommitSha,
    GitHubWorkflowState Result,
    string? WorkflowFile,
    string? WorkflowName,
    string? RunUrl,
    int? RunNumber,
    string? TriggeredBy,
    DateTimeOffset? StartedAtUtc,
    DateTimeOffset? CompletedAtUtc,
    DateTimeOffset RecordedAtUtc,
    IReadOnlyList<DeploymentEnvironmentData> Environments);

/// <summary>
/// An environment that was observed running this release's commit.
/// <para>
/// The link is evidence, not attribution: the environment's own version endpoint reported this commit.
/// A release with no entries was built but never seen running anywhere, which is a fact worth showing
/// rather than hiding.
/// </para>
/// </summary>
/// <param name="IsCurrent">
/// <c>true</c> when the environment's most recent version observation still reports this commit.
/// </param>
/// <param name="FirstObservedAtUtc">
/// First time this environment was seen reporting this commit. Console Ops treats this as the moment
/// the release became live here, because it is the first evidence it has.
/// </param>
/// <param name="HealthBefore">
/// Health observed for this environment on the last check before the release was seen, or <c>null</c>
/// when no earlier check exists.
/// </param>
/// <param name="HealthAfter">
/// Health observed for this environment on the first check at or after the release was seen, or
/// <c>null</c> when no such check exists.
/// </param>
public sealed record DeploymentEnvironmentData(
    Guid EnvironmentId,
    string EnvironmentName,
    string EnvironmentKind,
    bool IsCurrent,
    DateTimeOffset FirstObservedAtUtc,
    ApplicationHealthState? HealthBefore,
    DateTimeOffset? HealthBeforeObservedAtUtc,
    ApplicationHealthState? HealthAfter,
    DateTimeOffset? HealthAfterObservedAtUtc,
    VersionSyncState? VersionSync,
    DateTimeOffset? VersionSyncObservedAtUtc);
