namespace ConsoleOps.Api.Features.Dashboard;

public static class DashboardEndpoints
{
    public static IEndpointRouteBuilder MapDashboardEndpoints(this IEndpointRouteBuilder endpoints)
    {
        RouteGroupBuilder dashboard = endpoints.MapGroup("/api/dashboard")
            .WithTags("Dashboard");

        dashboard.MapGetDashboardOverviewEndpoint();
        return endpoints;
    }
}
