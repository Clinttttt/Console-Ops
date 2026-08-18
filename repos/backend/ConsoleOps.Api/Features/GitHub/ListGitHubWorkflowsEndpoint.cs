using ConsoleOps.Api.Extensions;
using ConsoleOps.Application.Abstractions.Messaging;
using ConsoleOps.Application.Features.GitHub.ListWorkflows;
using MediatR;

namespace ConsoleOps.Api.Features.GitHub;

internal static class ListGitHubWorkflowsEndpoint
{
    public static RouteGroupBuilder MapListGitHubWorkflowsEndpoint(this RouteGroupBuilder github)
    {
        github.MapGet("/repositories/{owner}/{repository}/workflows", Handle)
            .WithName("ListGitHubWorkflows")
            .WithSummary("Lists the GitHub Actions workflows configured in a repository.")
            .Produces<GitHubWorkflowsResponse>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status500InternalServerError);

        return github;
    }

    private static async Task<IResult> Handle(
        string owner,
        string repository,
        ISender sender,
        CancellationToken cancellationToken)
    {
        Result<GitHubWorkflowsResponse> result = await sender.Send(
            new ListGitHubWorkflowsQuery(owner, repository),
            cancellationToken);

        return result.ToHttpResult();
    }
}
