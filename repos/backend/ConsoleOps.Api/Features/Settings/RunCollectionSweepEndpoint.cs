using ConsoleOps.Api.BackgroundServices;
using ConsoleOps.Application.Integrations.Diagnostics;
using Microsoft.AspNetCore.RateLimiting;

namespace ConsoleOps.Api.Features.Settings;

/// <summary>
/// Collects now rather than waiting for the next scheduled sweep.
/// </summary>
/// <remarks>
/// The same sweep the worker runs, so an operator pressing this records exactly what the schedule would have.
/// It is a command rather than a read: it contacts every configured provider and writes observations, which is
/// why it is a POST and why it is rate limited.
/// </remarks>
internal static class RunCollectionSweepEndpoint
{
    public static RouteGroupBuilder MapRunCollectionSweepEndpoint(this RouteGroupBuilder settings)
    {
        settings.MapPost("/collection/sweeps", Handle)
            .WithName("RunCollectionSweep")
            .WithSummary("Runs a collection sweep now.")
            .WithDescription(
                "Refreshes every active project once, exactly as the scheduled sweep does, and reports how it "
                + "went. Contacts every configured provider, so it is bounded per client.")
            .RequireRateLimiting(SettingsEndpoints.SweepRateLimitPolicy)
            .Produces<CollectionSweepResponse>(StatusCodes.Status200OK);

        return settings;
    }

    private static async Task<IResult> Handle(
        ProjectCollectionSweeper sweeper,
        CancellationToken cancellationToken)
    {
        CollectionSweep sweep = await sweeper.SweepAsync(cancellationToken);

        return Results.Ok(new CollectionSweepResponse(
            sweep.CompletedAt,
            sweep.Succeeded,
            (int)sweep.Duration.TotalMilliseconds,
            sweep.ProjectsRefreshed,
            sweep.ProjectsFailed));
    }
}

/// <param name="Succeeded">
/// Whether the sweep completed. Projects can fail individually without failing the sweep, which is why the
/// counts are reported separately.
/// </param>
public sealed record CollectionSweepResponse(
    DateTimeOffset CompletedAt,
    bool Succeeded,
    int DurationMilliseconds,
    int ProjectsRefreshed,
    int ProjectsFailed);
