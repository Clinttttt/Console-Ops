namespace ConsoleOps.Application.Features.Dashboard.GetOverview;

public sealed record DashboardOverviewResponse(
    DateTimeOffset ObservedAt,
    DashboardPipelineResponse Pipeline,
    IReadOnlyList<DashboardProjectSurfaceResponse> Projects,
    DashboardSystemStateResponse SystemState,
    IReadOnlyList<DashboardActivityResponse> Activity,
    DashboardSystemSummaryResponse Summary);

public sealed record DashboardPipelineResponse(
    IReadOnlyList<DashboardPipelineStageResponse> Stages,
    DashboardStatusCellResponse Outcome);

public sealed record DashboardPipelineStageResponse(
    string Key,
    string Name,
    string Role,
    bool Verified);

public sealed record DashboardStatusCellResponse(
    string? Level,
    string Label,
    string? Detail);

public sealed record DashboardProjectSurfaceResponse(
    Guid Id,
    string Name,
    DashboardEnvironmentResponse Environment,
    DashboardSourceResponse Source,
    DashboardWorkflowResponse Workflow,
    DashboardStatusCellResponse Health,
    DateTimeOffset? HealthObservedAt,
    DashboardDeployedVersionResponse? DeployedVersion,
    DashboardVersionSyncResponse VersionSync,
    DashboardResponseMeasurementResponse Response);

public sealed record DashboardEnvironmentResponse(
    Guid Id,
    string Name,
    string Kind);

public sealed record DashboardSourceResponse(
    string Provider,
    string Repository,
    string Branch,
    string? CommitSha,
    string? CommitShortSha,
    DateTimeOffset? CommittedAt,
    DateTimeOffset? ObservedAt);

public sealed record DashboardWorkflowResponse(
    string Provider,
    string? WorkflowName,
    string State,
    string? CommitSha,
    DateTimeOffset? StartedAt,
    DateTimeOffset? CompletedAt,
    DateTimeOffset? ObservedAt);

public sealed record DashboardDeployedVersionResponse(
    string? Application,
    string? Version,
    string CommitSha,
    string CommitShortSha,
    string? Environment,
    DateTimeOffset? BuiltAt,
    DateTimeOffset ObservedAt);

public sealed record DashboardVersionSyncResponse(
    string State,
    string? SourceCommitSha,
    string? DeployedCommitSha,
    int? CommitsBehind,
    DateTimeOffset? ObservedAt);

public sealed record DashboardResponseMeasurementResponse(
    double? Milliseconds,
    IReadOnlyList<double> Samples,
    DateTimeOffset? ObservedAt);

public sealed record DashboardSystemStateResponse(
    IReadOnlyList<DashboardSystemStateColumnResponse> Columns,
    IReadOnlyList<DashboardSystemStateRowResponse> Rows);

public sealed record DashboardSystemStateColumnResponse(
    Guid ProjectId,
    string ProjectName,
    Guid EnvironmentId,
    string EnvironmentName);

public sealed record DashboardSystemStateRowResponse(
    string Key,
    string Label,
    IReadOnlyList<DashboardStatusCellResponse?> Cells);

public sealed record DashboardActivityResponse(
    Guid Id,
    string Kind,
    string Title,
    string? Context,
    DateTimeOffset OccurredAt);

public sealed record DashboardSystemSummaryResponse(
    string Level,
    string Label,
    DashboardUptimeWindowResponse? Uptime);

public sealed record DashboardUptimeWindowResponse(
    string Label,
    IReadOnlyList<double> Samples);
