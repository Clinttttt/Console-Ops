using ConsoleOps.Application.Features.Dashboard.GetOverview;
using MediatR;

namespace ConsoleOps.Api.Features.Dashboard;

internal static class GetDashboardOverviewEndpoint
{
    public static RouteGroupBuilder MapGetDashboardOverviewEndpoint(this RouteGroupBuilder dashboard)
    {
        dashboard.MapGet("/overview", Handle)
            .WithName("GetDashboardOverview")
            .Produces<DashboardOverviewResponse>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status500InternalServerError);

        return dashboard;
    }

    private static async Task<IResult> Handle(
        ISender sender,
        CancellationToken cancellationToken)
    {
        DashboardOverviewResponse response = await sender.Send(
            new GetDashboardOverviewQuery(),
            cancellationToken);
        return Results.Ok(response);
    }
}
