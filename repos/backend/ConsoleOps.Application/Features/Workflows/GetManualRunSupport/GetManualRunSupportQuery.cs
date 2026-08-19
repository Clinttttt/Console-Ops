using ConsoleOps.Application.Abstractions.Messaging;
using ConsoleOps.Application.Features.Projects;
using ConsoleOps.Application.Integrations.GitHub;
using MediatR;

namespace ConsoleOps.Application.Features.Workflows.GetManualRunSupport;

/// <summary>
/// Whether one workflow can be started manually.
/// </summary>
/// <remarks>
/// A separate read because the answer is in the workflow's own definition rather than in the listing, and it
/// costs one request per workflow. Reading it for every workflow on the page would double the cost of opening
/// the screen to answer a question about one of them, so the inventory reports <c>unknown</c> and this
/// establishes it for the workflow an operator selected.
/// </remarks>
public sealed record GetManualRunSupportQuery(Guid ProjectId, long WorkflowId, string WorkflowPath)
    : IRequest<Result<ManualRunSupportResponse>>;

/// <param name="ManualRun"><c>supported</c>, <c>unavailable</c>, or <c>unknown</c>.</param>
/// <param name="DefinitionPath">The file the answer was read from, so the claim can be checked.</param>
public sealed record ManualRunSupportResponse(string ManualRun, string DefinitionPath);

public sealed class GetManualRunSupportQueryHandler(
    IProjectReadStore projects,
    IGitHubWorkflowInventory inventory)
    : IRequestHandler<GetManualRunSupportQuery, Result<ManualRunSupportResponse>>
{
    public async Task<Result<ManualRunSupportResponse>> Handle(
        GetManualRunSupportQuery request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.WorkflowPath))
        {
            return Result<ManualRunSupportResponse>.Failure(WorkflowErrors.WorkflowPathRequired);
        }

        ProjectResponse? project = await projects.GetAsync(request.ProjectId, cancellationToken);
        if (project is null)
        {
            return Result<ManualRunSupportResponse>.Failure(WorkflowErrors.ProjectNotFound);
        }

        GitHubFactResult<GitHubManualRunSupport> result = await inventory.ReadManualRunSupportAsync(
            project.Repository.Owner,
            project.Repository.Name,
            request.WorkflowPath,
            cancellationToken);

        if (!result.IsSuccess)
        {
            return Result<ManualRunSupportResponse>.Failure(
                WorkflowErrors.From(result.Failure ?? GitHubReadFailure.Unavailable));
        }

        return Result<ManualRunSupportResponse>.Success(new ManualRunSupportResponse(
            ToManualRun(result.Observation!.SupportsManualRun),
            result.Observation.DefinitionPath));
    }

    /// <summary>
    /// Unknown stays unknown. Reporting it as unavailable would tell an operator a workflow cannot be run when
    /// Console Ops only failed to establish that it can.
    /// </summary>
    private static string ToManualRun(bool? supportsManualRun) => supportsManualRun switch
    {
        true => "supported",
        false => "unavailable",
        _ => "unknown"
    };
}
