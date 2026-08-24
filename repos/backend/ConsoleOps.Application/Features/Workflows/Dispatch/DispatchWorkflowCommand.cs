using ConsoleOps.Application.Abstractions.Messaging;
using ConsoleOps.Application.Abstractions.Persistence;
using ConsoleOps.Application.Features.Projects;
using ConsoleOps.Application.Integrations.GitHub;
using ConsoleOps.Domain.Projects;
using MediatR;

namespace ConsoleOps.Application.Features.Workflows.Dispatch;

/// <param name="Reference">
/// The branch or tag to run against. Required: a caller states it, because a dispatch that chose its own ref could
/// run against something an operator never looked at.
/// </param>
/// <param name="Confirmation">
/// The workflow's name, typed by the operator. Required only for a workflow marked destructive, and compared
/// against the name the provider reports rather than one the caller supplied.
/// </param>
public sealed record DispatchWorkflowCommand(
    Guid ProjectId,
    long WorkflowId,
    string Reference,
    IReadOnlyDictionary<string, string> Inputs,
    string? Confirmation)
    : IRequest<Result<WorkflowDispatchResponse>>;

/// <param name="Status">
/// Always <c>requested</c>. The provider accepts a dispatch without reporting a run, so Console Ops does not know
/// which run it started and says so rather than claiming one is running.
/// </param>
/// <param name="RequestedAt">
/// When the request was accepted. A caller finds the run by looking for one that started after this.
/// </param>
public sealed record WorkflowDispatchResponse(
    string Status,
    string WorkflowId,
    string Reference,
    DateTimeOffset RequestedAt);

/// <summary>
/// Starts a workflow, once everything that could refuse it has been checked here.
/// </summary>
/// <remarks>
/// The API is the authority, not the screen. Every gate is re-checked on this side: the project must own the
/// workflow, the provider must report the workflow, an operator must have marked its risk, a destructive workflow
/// needs its name typed, and the definition must declare a dispatch trigger. A screen that offered a run it should
/// not have would still be refused here.
/// </remarks>
public sealed class DispatchWorkflowCommandHandler(
    IProjectRepository projects,
    IGitHubWorkflowInventory inventory,
    TimeProvider timeProvider)
    : IRequestHandler<DispatchWorkflowCommand, Result<WorkflowDispatchResponse>>
{
    public async Task<Result<WorkflowDispatchResponse>> Handle(
        DispatchWorkflowCommand request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Reference))
        {
            return Result<WorkflowDispatchResponse>.Failure(WorkflowErrors.ReferenceRequired);
        }

        Project? project = await projects.GetActiveByIdAsync(request.ProjectId, cancellationToken);
        if (project is null)
        {
            return Result<WorkflowDispatchResponse>.Failure(ProjectErrors.NotFound);
        }

        // The provider's own description of the workflow, so the path whose risk marking applies is the real one.
        GitHubFactResult<GitHubWorkflowDefinition> lookup = await inventory.GetWorkflowAsync(
            project.RepositoryOwner,
            project.RepositoryName,
            request.WorkflowId,
            cancellationToken);

        if (!lookup.IsSuccess)
        {
            return Result<WorkflowDispatchResponse>.Failure(
                WorkflowErrors.From(lookup.Failure ?? GitHubReadFailure.Unavailable));
        }

        GitHubWorkflowDefinition workflow = lookup.Observation!;
        if (!workflow.Active)
        {
            return Result<WorkflowDispatchResponse>.Failure(WorkflowErrors.WorkflowDisabled);
        }

        WorkflowRiskLevel risk = project.RiskOf(workflow.Path);
        if (risk == WorkflowRiskLevel.Unclassified)
        {
            return Result<WorkflowDispatchResponse>.Failure(WorkflowErrors.RiskNotMarked);
        }

        if (risk == WorkflowRiskLevel.Destructive && !IsConfirmed(request.Confirmation, workflow.Name))
        {
            return Result<WorkflowDispatchResponse>.Failure(WorkflowErrors.ConfirmationRequired);
        }

        GitHubFactResult<GitHubManualRunSupport> support = await inventory.ReadManualRunSupportAsync(
            project.RepositoryOwner,
            project.RepositoryName,
            workflow.Path,
            cancellationToken);

        if (!support.IsSuccess)
        {
            return Result<WorkflowDispatchResponse>.Failure(
                WorkflowErrors.From(support.Failure ?? GitHubReadFailure.Unavailable));
        }

        if (support.Observation!.SupportsManualRun != true)
        {
            // Not established is refused as well as established-unavailable: starting a workflow whose definition
            // Console Ops could not read would be acting on a guess.
            return Result<WorkflowDispatchResponse>.Failure(WorkflowErrors.ManualRunUnavailable);
        }

        IReadOnlyDictionary<string, string> inputs = Accepted(request.Inputs, support.Observation.Inputs);

        GitHubDispatchResult dispatch = await inventory.DispatchAsync(
            project.RepositoryOwner,
            project.RepositoryName,
            workflow.WorkflowId,
            request.Reference.Trim(),
            inputs,
            cancellationToken);

        if (dispatch.Outcome != GitHubDispatchOutcome.Accepted)
        {
            return Result<WorkflowDispatchResponse>.Failure(ToError(dispatch));
        }

        return Result<WorkflowDispatchResponse>.Success(new WorkflowDispatchResponse(
            "requested",
            workflow.WorkflowId.ToString(System.Globalization.CultureInfo.InvariantCulture),
            request.Reference.Trim(),
            timeProvider.GetUtcNow()));
    }

    /// <summary>
    /// Whether the typed text is the workflow's name.
    /// </summary>
    /// <remarks>
    /// Trimmed and case-insensitive: the point is deliberate intent, not transcription accuracy, and a mismatch in
    /// capitalisation would only teach an operator to copy and paste.
    /// </remarks>
    private static bool IsConfirmed(string? confirmation, string workflowName) =>
        !string.IsNullOrWhiteSpace(confirmation)
        && string.Equals(confirmation.Trim(), workflowName.Trim(), StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Only inputs the workflow declared, so nothing invented is sent to the provider.
    /// </summary>
    /// <remarks>
    /// A declared input the caller left out is omitted rather than sent empty: the workflow's own default is the
    /// better answer, and an empty string is a value.
    /// </remarks>
    private static Dictionary<string, string> Accepted(
        IReadOnlyDictionary<string, string> supplied,
        IReadOnlyList<GitHubWorkflowInput> declared)
    {
        Dictionary<string, string> accepted = new(StringComparer.Ordinal);
        foreach (GitHubWorkflowInput input in declared)
        {
            if (supplied.TryGetValue(input.Name, out string? value) && !string.IsNullOrWhiteSpace(value))
            {
                accepted[input.Name] = value;
            }
        }

        return accepted;
    }

    /// <summary>
    /// The error for a refusal, carrying the provider's own wording where it gave any.
    /// </summary>
    /// <remarks>
    /// A rejected dispatch is the one failure Console Ops cannot explain on its own: only GitHub knows whether the
    /// ref was wrong, the trigger is missing on that ref, or an input was refused. Listing all three as
    /// possibilities sends an operator through all three.
    /// </remarks>
    private static Error ToError(GitHubDispatchResult dispatch)
    {
        Error error = dispatch.Outcome switch
        {
            GitHubDispatchOutcome.Forbidden => WorkflowErrors.DispatchUnauthorized,
            GitHubDispatchOutcome.NotFound => WorkflowErrors.From(GitHubReadFailure.NotFound),
            GitHubDispatchOutcome.Rejected => WorkflowErrors.DispatchRejected,
            GitHubDispatchOutcome.RateLimited => WorkflowErrors.From(GitHubReadFailure.RateLimited),
            _ => WorkflowErrors.From(GitHubReadFailure.Unavailable)
        };

        return dispatch.ProviderMessage is null
            ? error
            : new Error(error.Code, $"GitHub refused the run: {dispatch.ProviderMessage}", error.Type);
    }
}
