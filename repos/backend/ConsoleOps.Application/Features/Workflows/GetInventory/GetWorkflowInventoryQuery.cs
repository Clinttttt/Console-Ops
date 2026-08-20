using ConsoleOps.Application.Abstractions.Messaging;
using MediatR;

namespace ConsoleOps.Application.Features.Workflows.GetInventory;

/// <summary>
/// What automation exists across the registered repositories, and how each workflow last executed.
/// </summary>
/// <remarks>
/// Read live from the provider rather than from stored observations. The inventory is small, it changes whenever
/// a repository changes, and a stale workflow list is the more confusing failure: an operator asking "what
/// automation do I have" is asking about now. Each response states when it was read.
/// </remarks>
public sealed record GetWorkflowInventoryQuery : IRequest<Result<WorkflowInventoryResponse>>;

/// <param name="ReadAt">
/// When the provider was asked. Present because these are live facts with no observation record behind them.
/// </param>
public sealed record WorkflowInventoryResponse(
    DateTimeOffset ReadAt,
    IReadOnlyList<WorkflowProjectGroupResponse> Groups);

/// <param name="ReadFailure">
/// Why this project's workflows could not be read, or <c>null</c> when they were. One unreadable repository
/// leaves the other projects intact rather than emptying the screen, and it is never reported as a repository
/// with no automation.
/// </param>
public sealed record WorkflowProjectGroupResponse(
    Guid ProjectId,
    string ProjectName,
    string Repository,
    IReadOnlyList<WorkflowResponse> Workflows,
    string? ReadFailure);

/// <param name="Classification">
/// <c>deployment</c> or <c>unclassified</c>. A workflow is a deployment only where the operator registered that
/// file as this project's deployment workflow. No name, path, or trigger promotes a workflow to a type.
/// </param>
/// <param name="ManualRun">
/// <c>supported</c>, <c>unavailable</c>, or <c>unknown</c>. Unknown until the workflow definition is read, which
/// is where a dispatch trigger is declared.
/// </param>
/// <param name="Risk">
/// <c>unclassified</c>, <c>normal</c>, or <c>destructive</c>, as an operator marked it. Never derived: a name
/// cannot prove what a workflow does.
/// </param>
/// <param name="RiskDecidedAt">When an operator marked it, or <c>null</c> when nobody has.</param>
/// <param name="Executable">
/// Whether anything Console Ops has stored refuses to run this: <c>false</c> while the risk is unclassified, and
/// <c>false</c> for a workflow the provider has disabled.
/// <para>
/// It deliberately does not include whether the provider accepts a manual dispatch. That answer lives in the
/// workflow definition and is established for the workflow an operator selects, because reading it for every
/// workflow would double the cost of opening the screen. A run request is checked against both.
/// </para>
/// </param>
public sealed record WorkflowResponse(
    string Id,
    string Name,
    string Path,
    string State,
    string Classification,
    string ManualRun,
    string Risk,
    DateTimeOffset? RiskDecidedAt,
    bool Executable,
    WorkflowRunResponse? LatestRun);

/// <param name="Status">Where the run is: <c>queued</c>, <c>inProgress</c>, <c>waiting</c>, <c>completed</c>, or <c>unknown</c>.</param>
/// <param name="Conclusion">How it ended, or <c>null</c> while it has not ended.</param>
/// <param name="DurationSeconds">
/// Computed from the run's own start and end. <c>null</c> while it is still going, because a duration would
/// imply an end it has not reached.
/// </param>
/// <param name="Jobs">
/// Empty in the inventory. Jobs cost one request per run, so they are read for the workflow an operator selects
/// rather than for every workflow on the page.
/// </param>
public sealed record WorkflowRunResponse(
    string Id,
    int? Number,
    string Status,
    string? Conclusion,
    string Branch,
    string CommitSha,
    string CommitShortSha,
    string Trigger,
    string? Actor,
    DateTimeOffset? StartedAt,
    DateTimeOffset? CompletedAt,
    int? DurationSeconds,
    string? RunUrl,
    IReadOnlyList<WorkflowRunJobResponse> Jobs);

/// <param name="FailedStep">
/// The name of the step that failed, or <c>null</c> when none did.
/// </param>
/// <remarks>
/// Carried because "which job failed" is one question short of the useful one. It is the provider's own step
/// conclusion, not a guess: a job that failed without any step reporting a failure has no failing step to name.
/// </remarks>
public sealed record WorkflowRunJobResponse(
    string Name,
    string Status,
    string? Conclusion,
    int? DurationSeconds,
    string? FailedStep,
    IReadOnlyList<WorkflowRunStepResponse> Steps);

public sealed record WorkflowRunStepResponse(
    string Name,
    int? Number,
    string Status,
    string? Conclusion,
    int? DurationSeconds);
