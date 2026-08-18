using ConsoleOps.Api.Extensions;
using ConsoleOps.Application.Abstractions.Messaging;
using ConsoleOps.Application.Features.Health.GetOverview;
using MediatR;

namespace ConsoleOps.Api.Features.Health;

public static class HealthEndpoints
{
    public static IEndpointRouteBuilder MapHealthEndpoints(this IEndpointRouteBuilder endpoints)
    {
        RouteGroupBuilder health = endpoints.MapGroup("/api/health")
            .WithTags("Health");

        health.MapGetHealthOverviewEndpoint();
        return endpoints;
    }
}

internal static class GetHealthOverviewEndpoint
{
    public static RouteGroupBuilder MapGetHealthOverviewEndpoint(this RouteGroupBuilder health)
    {
        health.MapGet("/", Handle)
            .WithName("GetHealthOverview")
            .WithSummary("Reports the recorded health of every active environment.")
            .WithDescription(
                "Reads recorded observations only: the latest check per environment with the dependencies it "
                + "reported, the run that check belongs to, the availability window, and the transitions that "
                + "were recorded when they happened. No application is contacted.")
            .Produces<HealthOverviewResponse>(StatusCodes.Status200OK);

        return health;
    }

    private static async Task<IResult> Handle(ISender sender, CancellationToken cancellationToken)
    {
        Result<HealthOverviewResponse> result = await sender.Send(
            new GetHealthOverviewQuery(),
            cancellationToken);

        return result.ToHttpResult();
    }
}
