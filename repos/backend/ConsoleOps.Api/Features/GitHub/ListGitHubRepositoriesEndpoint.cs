using ConsoleOps.Api.Extensions;
using ConsoleOps.Application.Abstractions.Messaging;
using ConsoleOps.Application.Features.GitHub.ListRepositories;
using MediatR;

namespace ConsoleOps.Api.Features.GitHub;

internal static class ListGitHubRepositoriesEndpoint
{
    public static RouteGroupBuilder MapListGitHubRepositoriesEndpoint(this RouteGroupBuilder github)
    {
        github.MapGet("/repositories", Handle)
            .WithName("ListGitHubRepositories")
            .WithSummary("Lists repositories the configured GitHub credential can see.")
            .Produces<GitHubRepositoriesResponse>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status500InternalServerError);

        return github;
    }

    private static async Task<IResult> Handle(
        ISender sender,
        CancellationToken cancellationToken,
        string? query = null)
    {
        Result<GitHubRepositoriesResponse> result = await sender.Send(
            new ListGitHubRepositoriesQuery(query),
            cancellationToken);

        return result.ToHttpResult();
    }
}
