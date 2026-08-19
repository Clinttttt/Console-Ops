using ConsoleOps.Application.Abstractions.Messaging;
using ConsoleOps.Application.Features.Projects;
using ConsoleOps.Application.Features.Workflows.GetInventory;
using ConsoleOps.Application.Integrations.GitHub;
using MediatR;

namespace ConsoleOps.Application.Features.Workflows.GetRuns;

/// <summary>
/// Recent runs of one workflow, newest first.
/// </summary>
/// <remarks>
/// The repository comes from the registered project rather than from the caller, exactly as the job read does: a
/// run belongs to a repository, and letting a request name its own owner would let the browser point this read
/// at anything the token can see.
/// </remarks>
public sealed record GetWorkflowRunsQuery(Guid ProjectId, long WorkflowId, int? Limit)
    : IRequest<Result<WorkflowRunsResponse>>;

/// <param name="HasMore">
/// Whether the provider reported runs beyond this page, so the screen says this is recent history rather than
/// everything the workflow has done.
/// </param>
public sealed record WorkflowRunsResponse(
    string WorkflowId,
    IReadOnlyList<WorkflowRunResponse> Runs,
    bool HasMore);

public sealed class GetWorkflowRunsQueryHandler(
    IProjectReadStore projects,
    IGitHubWorkflowInventory inventory)
    : IRequestHandler<GetWorkflowRunsQuery, Result<WorkflowRunsResponse>>
{
    /// <summary>Enough to see a pattern without asking the provider to page through history.</summary>
    internal const int DefaultLimit = 20;

    internal const int MaximumLimit = 50;

    public async Task<Result<WorkflowRunsResponse>> Handle(
        GetWorkflowRunsQuery request,
        CancellationToken cancellationToken)
    {
        ProjectResponse? project = await projects.GetAsync(request.ProjectId, cancellationToken);
        if (project is null)
        {
            return Result<WorkflowRunsResponse>.Failure(WorkflowErrors.ProjectNotFound);
        }

        GitHubFactResult<GitHubRunPage> result = await inventory.ListRunsAsync(
            project.Repository.Owner,
            project.Repository.Name,
            request.WorkflowId,
            Bound(request.Limit),
            cancellationToken);

        if (!result.IsSuccess)
        {
            return Result<WorkflowRunsResponse>.Failure(
                WorkflowErrors.From(result.Failure ?? GitHubReadFailure.Unavailable));
        }

        WorkflowRunResponse[] runs = result.Observation!.Runs
            .Select(run => WorkflowRunMapping.ToRun(run)!)
            .ToArray();

        return Result<WorkflowRunsResponse>.Success(new WorkflowRunsResponse(
            request.WorkflowId.ToString(System.Globalization.CultureInfo.InvariantCulture),
            runs,
            result.Observation.HasMore));
    }

    /// <summary>
    /// Clamps what a caller asks for. A limit is a request, not an instruction: an unbounded one would let a
    /// query string decide how much of the provider's rate limit a single page load spends.
    /// </summary>
    private static int Bound(int? limit) => limit switch
    {
        null => DefaultLimit,
        < 1 => DefaultLimit,
        > MaximumLimit => MaximumLimit,
        _ => limit.Value
    };
}
