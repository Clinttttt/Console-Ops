using ConsoleOps.Application.Integrations.Diagnostics;
using Microsoft.Extensions.Options;

namespace ConsoleOps.Api.BackgroundServices;

/// <summary>
/// Remembers what the last retention sweep removed, for this process only.
/// </summary>
/// <remarks>
/// A single reference is swapped rather than fields being mutated, so a reader always sees one whole sweep rather
/// than half of two.
/// </remarks>
public sealed class RetentionJournal(IOptions<ObservationRetentionOptions> options) : IRetentionJournal
{
    private RetentionSweep? _lastSweep;

    public RetentionSchedule Schedule =>
        new(options.Value.Enabled, options.Value.Window, options.Value.Interval);

    public RetentionSweep? LastSweep => Volatile.Read(ref _lastSweep);

    public void Record(RetentionSweep sweep)
    {
        ArgumentNullException.ThrowIfNull(sweep);
        Volatile.Write(ref _lastSweep, sweep);
    }
}
