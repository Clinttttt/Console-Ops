using ConsoleOps.Application.Abstractions.Messaging;
using ConsoleOps.Application.Features.Projects;
using ConsoleOps.Application.Integrations.GitHub;
using MediatR;

namespace ConsoleOps.Application.Features.Workflows.GetBranches;

/// <summary>
/// The refs a run could target, so one is chosen from what exists rather than typed from memory.
/// </summary>
/// <remarks>
/// The repository comes from the registered project, as every other workflow read does. Read when the run panel is
/// opened rather than with the inventory: it is only needed to start something.
/// </remarks>
public sealed record GetWorkflowBranchesQuery(Guid ProjectId)
    : IRequest<Result<WorkflowBranchesResponse>>;

/// <param name="DefaultBranch">
/// The branch registered for the project, which a run defaults to. Always present, even when it is not in
/// <paramref name="Branches"/> - a registered branch that the provider no longer lists is worth showing as the
/// default rather than quietly dropping.
/// </param>
/// <param name="HasMore">Whether the repository has more branches than this bounded page.</param>
public sealed record WorkflowBranchesResponse(
    string DefaultBranch,
    IReadOnlyList<string> Branches,
    bool HasMore);

public sealed class GetWorkflowBranchesQueryHandler(
    IProjectReadStore projects,
    IGitHubRepositoryCatalog catalog)
    : IRequestHandler<GetWorkflowBranchesQuery, Result<WorkflowBranchesResponse>>
{
    public async Task<Result<WorkflowBranchesResponse>> Handle(
        GetWorkflowBranchesQuery request,
        CancellationToken cancellationToken)
    {
        ProjectResponse? project = await projects.GetAsync(request.ProjectId, cancellationToken);
        if (project is null)
        {
            return Result<WorkflowBranchesResponse>.Failure(ProjectErrors.NotFound);
        }

        GitHubFactResult<GitHubBranchList> result = await catalog.ListBranchesAsync(
            project.Repository.Owner,
            project.Repository.Name,
            cancellationToken);

        if (!result.IsSuccess)
        {
            return Result<WorkflowBranchesResponse>.Failure(
                WorkflowErrors.From(result.Failure ?? GitHubReadFailure.Unavailable));
        }

        // The registered branch is included even when the provider did not list it, so the default a run uses is
        // always selectable and an operator can see it is the one configured.
        List<string> branches = result.Observation!.Names.ToList();
        if (!branches.Contains(project.Repository.DefaultBranch, StringComparer.Ordinal))
        {
            branches.Insert(0, project.Repository.DefaultBranch);
        }

        return Result<WorkflowBranchesResponse>.Success(new WorkflowBranchesResponse(
            project.Repository.DefaultBranch,
            branches,
            result.Observation.HasMore));
    }
}
