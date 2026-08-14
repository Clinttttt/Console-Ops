using ConsoleOps.Application.Integrations.ApplicationMonitoring;
using ConsoleOps.Application.Integrations.GitHub;
using ConsoleOps.Domain.Monitoring;

namespace ConsoleOps.Infrastructure.Persistence.Monitoring;

public sealed class SourceObservationEntity
{
    public Guid Id { get; set; }
    public Guid ProjectId { get; set; }
    public bool IsAvailable { get; set; }
    public string Repository { get; set; } = string.Empty;
    public string DefaultBranch { get; set; } = string.Empty;
    public string? CommitSha { get; set; }
    public string? ShortCommitSha { get; set; }
    public DateTimeOffset? CommittedAtUtc { get; set; }
    public GitHubReadFailure? Failure { get; set; }
    public DateTimeOffset ObservedAtUtc { get; set; }
}

public sealed class WorkflowObservationEntity
{
    public Guid Id { get; set; }
    public Guid ProjectId { get; set; }
    public string? WorkflowFile { get; set; }
    public string? WorkflowName { get; set; }
    public GitHubWorkflowState State { get; set; }
    public string? CommitSha { get; set; }
    public DateTimeOffset? StartedAtUtc { get; set; }
    public DateTimeOffset? CompletedAtUtc { get; set; }
    public GitHubReadFailure? Failure { get; set; }
    public DateTimeOffset ObservedAtUtc { get; set; }
}

public sealed class HealthObservationEntity
{
    public Guid Id { get; set; }
    public Guid ProjectId { get; set; }
    public Guid EnvironmentId { get; set; }
    public string EnvironmentName { get; set; } = string.Empty;
    public string EnvironmentKind { get; set; } = string.Empty;
    public ApplicationHealthState State { get; set; }
    public double? ResponseMilliseconds { get; set; }
    public DateTimeOffset ObservedAtUtc { get; set; }
    public List<DependencyHealthObservationEntity> Dependencies { get; set; } = [];
}

public sealed class DependencyHealthObservationEntity
{
    public Guid Id { get; set; }
    public Guid HealthObservationId { get; set; }
    public string Name { get; set; } = string.Empty;
    public ApplicationHealthState State { get; set; }
}

public sealed class VersionObservationEntity
{
    public Guid Id { get; set; }
    public Guid ProjectId { get; set; }
    public Guid EnvironmentId { get; set; }
    public string EnvironmentName { get; set; } = string.Empty;
    public string EnvironmentKind { get; set; } = string.Empty;
    public ApplicationVersionState State { get; set; }
    public string? Application { get; set; }
    public string? Version { get; set; }
    public string? CommitSha { get; set; }
    public string? Environment { get; set; }
    public DateTimeOffset? BuiltAtUtc { get; set; }
    public DateTimeOffset ObservedAtUtc { get; set; }
}

public sealed class VersionSyncObservationEntity
{
    public Guid Id { get; set; }
    public Guid ProjectId { get; set; }
    public Guid EnvironmentId { get; set; }
    public string EnvironmentName { get; set; } = string.Empty;
    public string EnvironmentKind { get; set; } = string.Empty;
    public VersionSyncState State { get; set; }
    public int? CommitsBehind { get; set; }
    public DateTimeOffset ObservedAtUtc { get; set; }
}

public sealed class MonitoringActivityEntity
{
    public Guid Id { get; set; }
    public Guid ProjectId { get; set; }
    public Guid EnvironmentId { get; set; }
    public string EnvironmentName { get; set; } = string.Empty;
    public MonitoringActivityType Type { get; set; }
    public DateTimeOffset OccurredAtUtc { get; set; }
}
