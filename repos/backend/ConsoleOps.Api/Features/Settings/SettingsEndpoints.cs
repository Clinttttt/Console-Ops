using ConsoleOps.Api.Extensions;
using ConsoleOps.Application.Abstractions.Messaging;
using ConsoleOps.Application.Features.Settings.GetConfigurationStatus;
using MediatR;

namespace ConsoleOps.Api.Features.Settings;

public static class SettingsEndpoints
{
    /// <summary>
    /// Sweeping contacts every configured provider, so it is bounded per client like the other reads that do.
    /// </summary>
    public const string SweepRateLimitPolicy = "settings-sweep";

    public static IEndpointRouteBuilder MapSettingsEndpoints(this IEndpointRouteBuilder endpoints)
    {
        RouteGroupBuilder settings = endpoints.MapGroup("/api/settings")
            .WithTags("Settings");

        settings.MapGetConfigurationStatusEndpoint();
        settings.MapRunCollectionSweepEndpoint();
        return endpoints;
    }
}

internal static class GetConfigurationStatusEndpoint
{
    public static RouteGroupBuilder MapGetConfigurationStatusEndpoint(this RouteGroupBuilder settings)
    {
        settings.MapGet("/configuration", Handle)
            .WithName("GetConfigurationStatus")
            .WithSummary("Reports which configuration Console Ops has, by key name only.")
            .WithDescription(
                "Names and states only, never a value. Pass probe=true to also test each integration's "
                + "credentials, which contacts the providers and is therefore not the default.")
            .Produces<ConfigurationStatusResponse>(StatusCodes.Status200OK);

        return settings;
    }

    private static async Task<IResult> Handle(
        ISender sender,
        CancellationToken cancellationToken,
        bool probe = false)
    {
        Result<ConfigurationStatusResponse> result = await sender.Send(
            new GetConfigurationStatusQuery(probe),
            cancellationToken);

        return result.ToHttpResult();
    }
}
