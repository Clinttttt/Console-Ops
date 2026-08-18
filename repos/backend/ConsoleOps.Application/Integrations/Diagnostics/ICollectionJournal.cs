namespace ConsoleOps.Application.Integrations.Diagnostics;

/// <summary>
/// What Console Ops knows about its own collection: how it is scheduled, and how the last sweep went.
/// <para>
/// Kept in memory on purpose. A sweep is an event about this process, not an observation about a project, and
/// persisting it would put a second kind of record in the observation tables. It is therefore empty after a
/// restart, and the screen says "none since start-up" rather than inventing one.
/// </para>
/// </summary>
public interface ICollectionJournal
{
    CollectionSchedule Schedule { get; }

    /// <summary>The last completed sweep, or <c>null</c> when none has run since start-up.</summary>
    CollectionSweep? LastSweep { get; }

    void Record(CollectionSweep sweep);
}

/// <param name="IsEnabled">
/// Whether scheduled collection runs at all. Off is a deliberate configuration, not a fault: a deployment can
/// collect strictly on demand.
/// </param>
public sealed record CollectionSchedule(bool IsEnabled, TimeSpan Interval);

/// <param name="Succeeded">
/// Whether the sweep completed. Individual projects can fail without failing the sweep, which is why the
/// counts are reported separately.
/// </param>
/// <param name="ProjectsRefreshed">How many projects were refreshed successfully.</param>
/// <param name="ProjectsFailed">
/// How many did not. A sweep that succeeded with failures is a real state worth showing: the provider was
/// reachable for some projects and not others.
/// </param>
public sealed record CollectionSweep(
    DateTimeOffset StartedAt,
    DateTimeOffset CompletedAt,
    bool Succeeded,
    int ProjectsRefreshed,
    int ProjectsFailed)
{
    public TimeSpan Duration => CompletedAt - StartedAt;
}
