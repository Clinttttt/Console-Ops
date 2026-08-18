namespace ConsoleOps.Application.Integrations.Diagnostics;

/// <summary>
/// What Console Ops knows about its own retention: the window it keeps, and what the last sweep removed.
/// <para>
/// Retention is the only part of Console Ops that destroys recorded facts, so it reports what it did. In memory,
/// like the collection journal: it describes this process and is empty after a restart.
/// </para>
/// </summary>
public interface IRetentionJournal
{
    RetentionSchedule Schedule { get; }

    /// <summary>The last completed sweep, or <c>null</c> when none has run since start-up.</summary>
    RetentionSweep? LastSweep { get; }

    void Record(RetentionSweep sweep);
}

/// <param name="IsEnabled">Off means nothing is ever deleted, which a deployment may choose.</param>
/// <param name="Window">How much history is kept.</param>
public sealed record RetentionSchedule(bool IsEnabled, TimeSpan Window, TimeSpan Interval);

/// <param name="ObservationsRemoved">
/// How many rows the sweep deleted. Zero is a real answer: it means nothing had aged out yet.
/// </param>
/// <param name="Before">The cut-off the sweep used, so what was removed is unambiguous.</param>
public sealed record RetentionSweep(
    DateTimeOffset StartedAt,
    DateTimeOffset CompletedAt,
    bool Succeeded,
    int ObservationsRemoved,
    DateTimeOffset Before)
{
    public TimeSpan Duration => CompletedAt - StartedAt;
}
