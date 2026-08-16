namespace ConsoleOps.Api.Features.Deployments;

public static class DeploymentEndpoints
{
    public static IEndpointRouteBuilder MapDeploymentEndpoints(this IEndpointRouteBuilder endpoints)
    {
        RouteGroupBuilder deployments = endpoints.MapGroup("/api/deployments")
            .WithTags("Deployments");

        deployments.MapGetDeploymentHistoryEndpoint();
        return endpoints;
    }
}
