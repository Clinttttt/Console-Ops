using ConsoleOps.Application.Abstractions.Messaging;
using ConsoleOps.Application.Features.Projects;
using ConsoleOps.Application.Features.Projects.ListProjects;
using ConsoleOps.Application.Features.Projects.RefreshProject;
using MediatR;
using Microsoft.Extensions.Options;

namespace ConsoleOps.Api.BackgroundServices;

/// <summary>
/// Keeps observations current without the operator pressing anything.
/// <para>
/// This is the collector the manual refresh endpoint was written for: it sends the same
/// <see cref="RefreshProjectCommand"/>, so a background sweep and a button press produce identical
/// records and identical transition activity. There is no second code path that could disagree.
/// </para>
/// <para>
/// It also closes the gap in release history. Workflow runs are recorded from whatever GitHub reports at
/// refresh time, so with only manual refreshes a run that starts and finishes between two visits is
/// never seen. A steady sweep records those runs, and it is what gives the health-before and
/// health-after readings on the Deployments screen something to compare.
/// </para>
/// <para>
/// One failing project must not stop the sweep, and a failing sweep must not stop the worker: provider
/// failures are themselves observations, and Console Ops is more useful stale than dead.
/// </para>
/// </summary>
public sealed class ProjectRefreshWorker(
    IServiceScopeFactory scopeFactory,
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
                await SweepAsync(stoppingToken);
            }
            while (await timer.WaitForNextTickAsync(stoppingToken));
        }
        catch (OperationCanceledException)
        {
            // Shutdown, not a fault.
        }
    }

    private async Task SweepAsync(CancellationToken cancellationToken)
    {
        try
        {
            await using AsyncServiceScope scope = scopeFactory.CreateAsyncScope();
            ISender sender = scope.ServiceProvider.GetRequiredService<ISender>();
            ProjectResponse[] projects = await sender.Send(new ListProjectsQuery(), cancellationToken);

            for (int index = 0; index < projects.Length; index++)
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (index > 0 && _options.ProjectSpacing > TimeSpan.Zero)
                {
                    await Task.Delay(_options.ProjectSpacing, cancellationToken);
                }

                await RefreshAsync(sender, projects[index], cancellationToken);
            }
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception)
        {
            // A sweep that cannot even list projects is worth reporting, but never fatal.
            logger.LogError(exception, "A scheduled refresh sweep failed and will be retried.");
        }
    }

    private async Task RefreshAsync(
        ISender sender,
        ProjectResponse project,
        CancellationToken cancellationToken)
    {
        try
        {
            Result<RefreshProjectResponse> result = await sender.Send(
                new RefreshProjectCommand(project.Id),
                cancellationToken);

            if (!result.IsSuccess)
            {
                // Expected outcomes: the project was archived or reconfigured mid-sweep.
                logger.LogDebug(
                    "Scheduled refresh of project {ProjectId} did not apply: {ErrorCode}.",
                    project.Id,
                    result.Error.Code);
            }
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception)
        {
            logger.LogWarning(
                exception,
                "Scheduled refresh of project {ProjectId} failed; the sweep continues.",
                project.Id);
        }
    }
}
