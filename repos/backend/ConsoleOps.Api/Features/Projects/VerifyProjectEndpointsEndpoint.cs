using ConsoleOps.Api.Extensions;
using ConsoleOps.Application.Abstractions.Messaging;
using ConsoleOps.Application.Features.Projects.VerifyEndpoints;
using MediatR;

namespace ConsoleOps.Api.Features.Projects;

internal static class VerifyProjectEndpointsEndpoint
{
    /// <summary>Rate limiter policy name; this endpoint accepts operator-supplied targets.</summary>
    internal const string RateLimitPolicy = "endpoint-verification";

    public static RouteGroupBuilder MapVerifyProjectEndpointsEndpoint(this RouteGroupBuilder projects)
    {
        projects.MapPost("/verification", Handle)
            .WithName("VerifyProjectEndpoints")
            .WithSummary("Probes candidate health and version endpoints before a project is registered.")
            .RequireRateLimiting(RateLimitPolicy)
            .Produces<EndpointVerificationResponse>(StatusCodes.Status200OK)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status429TooManyRequests)
            .ProducesProblem(StatusCodes.Status500InternalServerError);

        return projects;
    }

    private static async Task<IResult> Handle(
        VerifyProjectEndpointsRequest request,
        ISender sender,
        CancellationToken cancellationToken)
    {
        Result<EndpointVerificationResponse> result = await sender.Send(
            new VerifyProjectEndpointsCommand(request.HealthUrl, request.VersionUrl),
            cancellationToken);

        return result.ToHttpResult();
    }
}

/// <summary>Absolute health and version URLs to probe. Either may be omitted, but not both.</summary>
internal sealed record VerifyProjectEndpointsRequest(string? HealthUrl, string? VersionUrl);
