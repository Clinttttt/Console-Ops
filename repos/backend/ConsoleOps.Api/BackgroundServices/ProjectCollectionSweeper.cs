using ConsoleOps.Application.Abstractions.Messaging;
using ConsoleOps.Application.Features.Projects;
using ConsoleOps.Application.Features.Projects.ListProjects;
using ConsoleOps.Application.Features.Projects.RefreshProject;
using ConsoleOps.Application.Integrations.Diagnostics;
using MediatR;
using Microsoft.Extensions.Options;

namespace ConsoleOps.Api.BackgroundServices;

/// <summary>
/// Refreshes every active project once, and records how it went.
/// <para>
/// One sweep implementation for both callers: the scheduled worker and the operator pressing Refresh now. A
/// second implementation would eventually disagree with the first about what a sweep is, and the whole point of
/// sending the same <see cref="RefreshProjectCommand"/> as the manual endpoint is that no two paths can record
/// different facts.
/// </para>
/// <para>
/// One failing project never fails the sweep. Provider failures are themselves observations, and the counts
/// are reported so a partly successful sweep reads as exactly that.
/// </para>
/// </summary>
public sealed class ProjectCollectionSweeper(
    IServiceScopeFactory scopeFactory,
    CollectionJournal journal,
    IOptions<ProjectRefreshOptions> options,
    ILogger<ProjectCollectionSweeper> logger)
{
    private readonly ProjectRefreshOptions _options = options.Value;

    public async Task<CollectionSweep> SweepAsync(CancellationToken cancellationToken)
    {
        DateTimeOffset startedAt = journal.StartSweep();
        int refreshed = 0;
        int failed = 0;
        bool succeeded = true;

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

                if (await RefreshAsync(sender, projects[index], cancellationToken))
                {
                    refreshed++;
                }
                else
                {
                    failed++;
                }
            }
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception)
        {
            // A sweep that cannot even list projects is worth reporting, but never fatal.
            succeeded = false;
            logger.LogError(exception, "A collection sweep failed and will be retried.");
        }

        CollectionSweep sweep = new(startedAt, journal.Now(), succeeded, refreshed, failed);
        journal.Record(sweep);
        return sweep;
    }

    private async Task<bool> RefreshAsync(
        ISender sender,
        ProjectResponse project,
        CancellationToken cancellationToken)
    {
        try
        {
            Result<RefreshProjectResponse> result = await sender.Send(
                new RefreshProjectCommand(project.Id),
                cancellationToken);

            if (result.IsSuccess)
            {
                return true;
            }

            // Expected outcomes: the project was archived or reconfigured mid-sweep.
            logger.LogDebug(
                "Collection of project {ProjectId} did not apply: {ErrorCode}.",
                project.Id,
                result.Error.Code);
            return false;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception)
        {
            logger.LogWarning(
                exception,
                "Collection of project {ProjectId} failed; the sweep continues.",
                project.Id);
            return false;
        }
    }
}
