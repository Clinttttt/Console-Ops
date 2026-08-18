using ConsoleOps.Api.Extensions;
using ConsoleOps.Application.Abstractions.Messaging;
using ConsoleOps.Application.Features.Azure.ListLogSources;
using MediatR;

namespace ConsoleOps.Api.Features.Azure;

public static class AzureEndpoints
{
    public static IEndpointRouteBuilder MapAzureEndpoints(this IEndpointRouteBuilder endpoints)
    {
        RouteGroupBuilder azure = endpoints.MapGroup("/api/azure")
            .WithTags("Azure");

        azure.MapListAzureLogSourcesEndpoint();
        return endpoints;
    }
}

internal static class ListAzureLogSourcesEndpoint
{
    public static RouteGroupBuilder MapListAzureLogSourcesEndpoint(this RouteGroupBuilder azure)
    {
        azure.MapGet("/log-sources", Handle)
            .WithName("ListAzureLogSources")
            .WithSummary("Lists container apps the configured Azure identity can see, with their log workspace.")
            .Produces<AzureLogSourcesResponse>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status500InternalServerError);

        return azure;
    }

    private static async Task<IResult> Handle(
        ISender sender,
        CancellationToken cancellationToken,
        string? query = null)
    {
        Result<AzureLogSourcesResponse> result = await sender.Send(
            new ListAzureLogSourcesQuery(query),
            cancellationToken);

        return result.ToHttpResult();
    }
}
