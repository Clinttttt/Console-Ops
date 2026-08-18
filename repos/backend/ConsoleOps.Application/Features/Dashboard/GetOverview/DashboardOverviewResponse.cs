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

/// <param name="DeployedVersion">
/// The version an environment reported, or <c>null</c> when none was read. Null alone does not say why, which is
/// what <paramref name="VersionState"/> is for.
/// </param>
/// <param name="VersionState">
/// <c>available</c>, <c>unknown</c>, or <c>notConfigured</c>. Sent because the screen previously rendered every
/// missing version as "Not configured", which told an operator to fix configuration that was already correct: an
/// endpoint answering 401, or answering with HTML, is configured and unreadable, not unconfigured.
/// </param>
public sealed record DashboardProjectSurfaceResponse(
    Guid Id,
    string Name,
    DashboardEnvironmentResponse Environment,
    DashboardSourceResponse Source,
    DashboardWorkflowResponse Workflow,
    DashboardStatusCellResponse Health,
    DateTimeOffset? HealthObservedAt,
    DashboardDeployedVersionResponse? DeployedVersion,
    string VersionState,
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

/// <summary>
/// Availability observed by Console Ops over the window, or <c>null</c> when too few checks exist for a
/// figure to mean anything.
/// </summary>
/// <param name="WindowHours">Length of the window. The UI decides how to word it.</param>
/// <param name="Percentage">Share of measured checks that were acceptable, to one decimal.</param>
/// <param name="Checks">Measured checks behind the figure, so the screen can say what it rests on.</param>
/// <param name="Samples">
/// Availability per hour, oldest first, for hours containing checks. Hours with no check are absent
/// rather than drawn as zero.
/// </param>
public sealed record DashboardUptimeWindowResponse(
    int WindowHours,
    DateTimeOffset Since,
    double Percentage,
    int Checks,
    IReadOnlyList<double> Samples);
