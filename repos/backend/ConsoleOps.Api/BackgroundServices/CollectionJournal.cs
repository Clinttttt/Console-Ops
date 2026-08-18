using ConsoleOps.Application.Integrations.Diagnostics;
using Microsoft.Extensions.Options;

namespace ConsoleOps.Api.BackgroundServices;

/// <summary>
/// Remembers how the last collection sweep went, for this process only.
/// </summary>
/// <remarks>
/// A single reference is swapped rather than fields being mutated, so a reader always sees one whole sweep
/// rather than half of two. Registered as a singleton because it describes the process, not a request.
/// </remarks>
public sealed class CollectionJournal(
    IOptions<ProjectRefreshOptions> options,
    TimeProvider timeProvider) : ICollectionJournal
{
    private CollectionSweep? _lastSweep;

    public CollectionSchedule Schedule => new(options.Value.Enabled, options.Value.Interval);

    public CollectionSweep? LastSweep => Volatile.Read(ref _lastSweep);

    public void Record(CollectionSweep sweep)
    {
        ArgumentNullException.ThrowIfNull(sweep);
        Volatile.Write(ref _lastSweep, sweep);
    }

    /// <summary>
    /// Starts timing a sweep. The journal owns the clock so a sweep's duration cannot be measured against a
    /// different one than its timestamps.
    /// </summary>
    public DateTimeOffset StartSweep() => timeProvider.GetUtcNow();

    public DateTimeOffset Now() => timeProvider.GetUtcNow();
}
