using ConsoleOps.Application.Features.Deployments.GetHistory;
using MediatR;

namespace ConsoleOps.Api.Features.Deployments;

internal static class GetDeploymentHistoryEndpoint
{
    public static RouteGroupBuilder MapGetDeploymentHistoryEndpoint(
        this RouteGroupBuilder deployments)
    {
        deployments.MapGet("/", Handle)
            .WithName("GetDeploymentHistory")
            .Produces<DeploymentHistoryResponse>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status500InternalServerError);

        return deployments;
    }

    private static async Task<IResult> Handle(
        ISender sender,
        CancellationToken cancellationToken,
        int? limit = null)
    {
        DeploymentHistoryResponse response = await sender.Send(
            new GetDeploymentHistoryQuery(limit),
            cancellationToken);
        return Results.Ok(response);
    }
}
