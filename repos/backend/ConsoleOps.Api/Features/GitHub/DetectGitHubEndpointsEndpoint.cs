using ConsoleOps.Api.Extensions;
using ConsoleOps.Application.Abstractions.Messaging;
using ConsoleOps.Application.Features.GitHub.DetectEndpoints;
using MediatR;

namespace ConsoleOps.Api.Features.GitHub;

internal static class DetectGitHubEndpointsEndpoint
{
    public static RouteGroupBuilder MapDetectGitHubEndpointsEndpoint(this RouteGroupBuilder github)
    {
        github.MapGet("/repositories/{owner}/{repository}/endpoints", Handle)
            .WithName("DetectGitHubEndpoints")
            .WithSummary("Suggests health and version endpoint paths found in repository source.")
            .Produces<GitHubDetectedEndpointsResponse>(StatusCodes.Status200OK)
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
        Result<GitHubDetectedEndpointsResponse> result = await sender.Send(
            new DetectGitHubEndpointsQuery(owner, repository, branch),
            cancellationToken);

        return result.ToHttpResult();
    }
}
