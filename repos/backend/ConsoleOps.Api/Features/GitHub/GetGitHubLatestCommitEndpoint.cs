using ConsoleOps.Api.Extensions;
using ConsoleOps.Application.Abstractions.Messaging;
using ConsoleOps.Application.Features.GitHub.GetLatestCommit;
using MediatR;

namespace ConsoleOps.Api.Features.GitHub;

internal static class GetGitHubLatestCommitEndpoint
{
    public static RouteGroupBuilder MapGetGitHubLatestCommitEndpoint(this RouteGroupBuilder github)
    {
        github.MapGet("/repositories/{owner}/{repository}/commits/latest", Handle)
            .WithName("GetGitHubLatestCommit")
            .WithSummary("Reads the head commit of a branch.")
            .Produces<GitHubLatestCommitResponse>(StatusCodes.Status200OK)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status500InternalServerError);

        return github;
    }

    private static async Task<IResult> Handle(
        string owner,
        string repository,
        string branch,
        ISender sender,
        CancellationToken cancellationToken)
    {
        Result<GitHubLatestCommitResponse> result = await sender.Send(
            new GetGitHubLatestCommitQuery(owner, repository, branch),
            cancellationToken);

        return result.ToHttpResult();
    }
}
