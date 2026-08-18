using ConsoleOps.Application.Integrations.Diagnostics;
using ConsoleOps.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace ConsoleOps.Api.BackgroundServices;

/// <summary>
/// Deletes observations older than the retention window.
/// <para>
/// Collection appends a row per environment per sweep, and every screen reads a bounded window, so rows past
/// that window cost storage without answering anything. Deleting them is the only part of Console Ops that
/// destroys recorded facts, which is why the window is configuration, the floor is a week, and every run reports
/// what it removed.
/// </para>
/// <para>
/// Deletes are batched and each table is handled separately: a first run against a long-neglected database must
/// not hold a lock long enough to stall collection.
/// </para>
/// </summary>
public sealed class ObservationRetentionWorker(
    IServiceScopeFactory scopeFactory,
    IRetentionJournal journal,
    IOptions<ObservationRetentionOptions> options,
    TimeProvider timeProvider,
    ILogger<ObservationRetentionWorker> logger) : BackgroundService
{
    private readonly ObservationRetentionOptions _options = options.Value;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation(
            "Observation retention is enabled: keeping {Days} days, sweeping every {IntervalHours}h.",
            _options.Window.TotalDays,
            _options.Interval.TotalHours);

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
        DateTimeOffset startedAt = timeProvider.GetUtcNow();
        DateTimeOffset before = startedAt - _options.Window;
        int removed = 0;

        try
        {
            await using AsyncServiceScope scope = scopeFactory.CreateAsyncScope();
            ConsoleOpsDbContext dbContext = scope.ServiceProvider.GetRequiredService<ConsoleOpsDbContext>();

            // Health observations first: their dependency rows go with them through the database's own cascade,
            // so nothing has to delete those separately or risk orphaning them.
            removed += await DeleteAsync(
                dbContext,
                dbContext.HealthObservations.Where(row => row.ObservedAtUtc < before).Select(row => row.Id),
                ids => dbContext.HealthObservations.Where(row => ids.Contains(row.Id)),
                cancellationToken);

            removed += await DeleteAsync(
                dbContext,
                dbContext.VersionObservations.Where(row => row.ObservedAtUtc < before).Select(row => row.Id),
                ids => dbContext.VersionObservations.Where(row => ids.Contains(row.Id)),
                cancellationToken);

            removed += await DeleteAsync(
                dbContext,
                dbContext.VersionSyncObservations.Where(row => row.ObservedAtUtc < before).Select(row => row.Id),
                ids => dbContext.VersionSyncObservations.Where(row => ids.Contains(row.Id)),
                cancellationToken);

            removed += await DeleteAsync(
                dbContext,
                dbContext.SourceObservations.Where(row => row.ObservedAtUtc < before).Select(row => row.Id),
                ids => dbContext.SourceObservations.Where(row => ids.Contains(row.Id)),
                cancellationToken);

            removed += await DeleteAsync(
                dbContext,
                dbContext.WorkflowObservations.Where(row => row.ObservedAtUtc < before).Select(row => row.Id),
                ids => dbContext.WorkflowObservations.Where(row => ids.Contains(row.Id)),
                cancellationToken);

            removed += await DeleteAsync(
                dbContext,
                dbContext.MonitoringActivities.Where(row => row.OccurredAtUtc < before).Select(row => row.Id),
                ids => dbContext.MonitoringActivities.Where(row => ids.Contains(row.Id)),
                cancellationToken);

            journal.Record(new RetentionSweep(startedAt, timeProvider.GetUtcNow(), true, removed, before));
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception)
        {
            // A failed sweep is reported and retried. Nothing else depends on it having run.
            journal.Record(new RetentionSweep(startedAt, timeProvider.GetUtcNow(), false, removed, before));
            logger.LogError(exception, "An observation retention sweep failed and will be retried.");
        }
    }

    /// <summary>
    /// Deletes in batches until the table has nothing older than the window.
    /// </summary>
    /// <remarks>
    /// Ids are selected and then deleted by id rather than translating a bounded delete, so the statement is the
    /// same shape on any provider and each batch is exactly the size configured.
    /// </remarks>
    private async Task<int> DeleteAsync(
        ConsoleOpsDbContext dbContext,
        IQueryable<Guid> expired,
        Func<Guid[], IQueryable<object>> rowsFor,
        CancellationToken cancellationToken)
    {
        int removed = 0;

        while (!cancellationToken.IsCancellationRequested)
        {
            Guid[] batch = await expired.Take(_options.Batch).ToArrayAsync(cancellationToken);
            if (batch.Length == 0)
            {
                return removed;
            }

            removed += await rowsFor(batch).ExecuteDeleteAsync(cancellationToken);

            if (batch.Length < _options.Batch)
            {
                return removed;
            }
        }

        return removed;
    }
}
