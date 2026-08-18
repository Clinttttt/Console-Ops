using ConsoleOps.Application.Features.Projects.RefreshProject;
using Microsoft.Extensions.Options;

namespace ConsoleOps.Api.BackgroundServices;

/// <summary>
/// Keeps observations current without the operator pressing anything.
/// <para>
/// The sweep itself lives in <see cref="ProjectCollectionSweeper"/>, shared with the on-demand endpoint, so a
/// background sweep and a button press produce identical records and identical transition activity. There is no
/// second code path that could disagree.
/// </para>
/// <para>
/// It also closes the gap in release history. Workflow runs are recorded from whatever GitHub reports at
/// refresh time, so with only manual refreshes a run that starts and finishes between two visits is never
/// seen. A steady sweep records those runs, and it is what gives the health-before and health-after readings
/// on the Deployments screen something to compare.
/// </para>
/// <para>
/// A failing sweep must not stop the worker: provider failures are themselves observations, and Console Ops is
/// more useful stale than dead.
/// </para>
/// </summary>
public sealed class ProjectRefreshWorker(
    ProjectCollectionSweeper sweeper,
    IOptions<ProjectRefreshOptions> options,
    ILogger<ProjectRefreshWorker> logger) : BackgroundService
{
    private readonly ProjectRefreshOptions _options = options.Value;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation(
            "Automatic project refresh is enabled every {IntervalSeconds}s.",
            _options.Interval.TotalSeconds);

        try
        {
            await Task.Delay(_options.StartupDelay, stoppingToken);

            using PeriodicTimer timer = new(_options.Interval);
            do
            {
                await sweeper.SweepAsync(stoppingToken);
            }
            while (await timer.WaitForNextTickAsync(stoppingToken));
        }
        catch (OperationCanceledException)
        {
            // Shutdown, not a fault.
        }
    }
}
