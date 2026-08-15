namespace ConsoleOps.Api.Features.GitHub;

public static class GitHubEndpoints
{
    public static IEndpointRouteBuilder MapGitHubEndpoints(this IEndpointRouteBuilder endpoints)
    {
        RouteGroupBuilder github = endpoints.MapGroup("/api/github")
            .WithTags("GitHub");

        github.MapListGitHubRepositoriesEndpoint();
        github.MapListGitHubWorkflowsEndpoint();
        github.MapGetGitHubLatestCommitEndpoint();
        github.MapDetectGitHubEndpointsEndpoint();
        return endpoints;
    }
}
