using ConsoleOps.Api.Extensions;
using ConsoleOps.Application.Abstractions.Messaging;
using ConsoleOps.Application.Features.Logs.GetStream;
using MediatR;
using Microsoft.AspNetCore.RateLimiting;

namespace ConsoleOps.Api.Features.Logs;

public static class LogEndpoints
{
    /// <summary>
    /// Each read asks a provider, so the endpoint is bounded per client as well as per query.
    /// </summary>
    public const string RateLimitPolicy = "logs-read";

    public static IEndpointRouteBuilder MapLogEndpoints(this IEndpointRouteBuilder endpoints)
    {
        RouteGroupBuilder logs = endpoints.MapGroup("/api/logs")
            .WithTags("Logs");

        logs.MapGetLogStreamEndpoint();
        return endpoints;
    }
}

internal static class GetLogStreamEndpoint
{
    public static RouteGroupBuilder MapGetLogStreamEndpoint(this RouteGroupBuilder logs)
    {
        logs.MapGet("/", Handle)
            .WithName("GetLogStream")
            .WithSummary("Reads one project environment's application logs from its provider.")
            .RequireRateLimiting(LogEndpoints.RateLimitPolicy)
            .Produces<LogStreamResponse>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status500InternalServerError);

        return logs;
    }

    private static async Task<IResult> Handle(
        ISender sender,
        CancellationToken cancellationToken,
        Guid? projectId = null,
        Guid? environmentId = null,
        string? search = null,
        DateTimeOffset? before = null,
        int? limit = null)
    {
        Result<LogStreamResponse> result = await sender.Send(
            new GetLogStreamQuery(projectId, environmentId, search, before, limit),
            cancellationToken);

        return result.ToHttpResult();
    }
}
