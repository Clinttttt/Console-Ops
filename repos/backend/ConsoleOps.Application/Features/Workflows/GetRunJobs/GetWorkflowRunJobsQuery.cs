using ConsoleOps.Application.Abstractions.Messaging;
using ConsoleOps.Application.Features.Projects;
using ConsoleOps.Application.Features.Workflows.GetInventory;
using ConsoleOps.Application.Integrations.GitHub;
using MediatR;

namespace ConsoleOps.Application.Features.Workflows.GetRunJobs;

/// <summary>
/// The jobs of one run, read for the workflow an operator selected.
/// </summary>
/// <remarks>
/// Separate from the inventory because jobs cost one request per run. Reading them for every workflow on the
/// page would multiply the cost of opening the screen to answer a question nobody asked yet.
/// </remarks>
public sealed record GetWorkflowRunJobsQuery(Guid ProjectId, long RunId)
    : IRequest<Result<WorkflowRunJobsResponse>>;

public sealed record WorkflowRunJobsResponse(
    string RunId,
    IReadOnlyList<WorkflowRunJobResponse> Jobs);

public sealed class GetWorkflowRunJobsQueryHandler(
    IProjectReadStore projects,
    IGitHubWorkflowInventory inventory)
    : IRequestHandler<GetWorkflowRunJobsQuery, Result<WorkflowRunJobsResponse>>
{
    public async Task<Result<WorkflowRunJobsResponse>> Handle(
        GetWorkflowRunJobsQuery request,
        CancellationToken cancellationToken)
    {
        // The repository comes from the registered project rather than from the caller: a run belongs to a
        // repository, and letting a request name its own owner would let the browser point this read anywhere.
        ProjectResponse? project = await projects.GetAsync(request.ProjectId, cancellationToken);
        if (project is null)
        {
            return Result<WorkflowRunJobsResponse>.Failure(WorkflowErrors.ProjectNotFound);
        }

        GitHubFactResult<GitHubRunJobs> result = await inventory.ListRunJobsAsync(
            project.Repository.Owner,
            project.Repository.Name,
            request.RunId,
            cancellationToken);

        if (!result.IsSuccess)
        {
            return Result<WorkflowRunJobsResponse>.Failure(
                WorkflowErrors.From(result.Failure ?? GitHubReadFailure.Unavailable));
        }

        WorkflowRunJobResponse[] jobs = result.Observation!.Jobs
            .Select(job => new WorkflowRunJobResponse(
                job.Name,
                ToCamelCase(job.Status),
                job.Conclusion is null ? null : ToCamelCase(job.Conclusion.Value),
                DurationSeconds(job.StartedAtUtc, job.CompletedAtUtc)))
            .ToArray();

        return Result<WorkflowRunJobsResponse>.Success(new WorkflowRunJobsResponse(
            request.RunId.ToString(System.Globalization.CultureInfo.InvariantCulture),
            jobs));
    }

    private static int? DurationSeconds(DateTimeOffset? startedAt, DateTimeOffset? completedAt)
    {
        if (startedAt is null || completedAt is null || completedAt < startedAt)
        {
            return null;
        }

        return (int)(completedAt.Value - startedAt.Value).TotalSeconds;
    }

    private static string ToCamelCase<TEnum>(TEnum value)
        where TEnum : struct, Enum
    {
        string name = value.ToString()!;
        return string.Concat(char.ToLowerInvariant(name[0]), name[1..]);
    }
}
