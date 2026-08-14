namespace ConsoleOps.Application.Features.Projects.RefreshProject;

public sealed record RefreshProjectResponse(
    Guid ProjectId,
    DateTimeOffset RefreshedAtUtc,
    RefreshSourceResponse Source,
    RefreshWorkflowResponse Workflow,
    IReadOnlyList<RefreshEnvironmentResponse> Environments,
    IReadOnlyList<RefreshActivityResponse> Activities);

public sealed record RefreshSourceResponse(
    string State,
    string Repository,
    string DefaultBranch,
    string? CommitSha,
    string? ShortCommitSha,
    DateTimeOffset? CommittedAtUtc,
    DateTimeOffset ObservedAtUtc);

public sealed record RefreshWorkflowResponse(
    string Provider,
    string? WorkflowFile,
    string? WorkflowName,
    string State,
    string? CommitSha,
    DateTimeOffset? StartedAtUtc,
    DateTimeOffset? CompletedAtUtc,
    DateTimeOffset ObservedAtUtc);

public sealed record RefreshEnvironmentResponse(
    Guid EnvironmentId,
    string EnvironmentName,
    string EnvironmentKind,
    RefreshHealthResponse Health,
    RefreshVersionResponse Version,
    RefreshVersionSyncResponse VersionSync);

public sealed record RefreshHealthResponse(
    string State,
    double? ResponseMilliseconds,
    DateTimeOffset ObservedAtUtc,
    IReadOnlyList<RefreshDependencyResponse> Dependencies);

public sealed record RefreshDependencyResponse(string Name, string State);

public sealed record RefreshVersionResponse(
    string State,
    string? Application,
    string? Version,
    string? CommitSha,
    string? Environment,
    DateTimeOffset? BuiltAtUtc,
    DateTimeOffset ObservedAtUtc);

public sealed record RefreshVersionSyncResponse(
    string State,
    int? CommitsBehind,
    DateTimeOffset ObservedAtUtc);

public sealed record RefreshActivityResponse(
    Guid EnvironmentId,
    string EnvironmentName,
    string Type,
    DateTimeOffset OccurredAtUtc);
