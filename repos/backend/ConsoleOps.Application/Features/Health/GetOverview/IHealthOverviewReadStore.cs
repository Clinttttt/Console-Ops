using ConsoleOps.Application.Integrations.ApplicationMonitoring;
using ConsoleOps.Domain.Monitoring;

namespace ConsoleOps.Application.Features.Health.GetOverview;

/// <summary>
/// Reads the recorded health of every active environment.
/// <para>
/// Reads only. Health reports what collection already wrote: the latest check per environment with its
/// dependencies, the run that check belongs to, the availability window, and the transitions that were recorded
/// at the moment they happened. Nothing here contacts an application.
/// </para>
/// </summary>
public interface IHealthOverviewReadStore
{
    Task<HealthOverviewData> ReadAsync(int windowHours, int transitionCount, CancellationToken cancellationToken);
}

public sealed record HealthOverviewData(
    IReadOnlyList<EnvironmentHealthData> Environments,
    IReadOnlyList<HealthTransitionData> Transitions);

/// <param name="Checked">
/// The latest check, or <c>null</c> when an environment has never been checked - which is why a row can exist
/// with no verdict at all.
/// </param>
/// <param name="HealthySince">
/// When the current unbroken healthy run began, or <c>null</c> when the environment is not healthy now. Derived
/// from recorded observations rather than stored, because a run is a property of the sequence.
/// </param>
/// <param name="FailingSince">When the current unbroken failing run began, or <c>null</c> when it is not failing.</param>
/// <param name="ConsecutiveFailures">
/// How many checks in a row have failed, ending at the latest. Zero when the latest check did not fail.
/// </param>
/// <param name="LastHealthyAt">
/// The most recent healthy check, or <c>null</c> when there has never been one inside the window read.
/// </param>
/// <param name="Uptime">
/// The availability window, or <c>null</c> when too few checks were recorded to report one. Never a figure
/// computed from a handful of checks.
/// </param>
public sealed record EnvironmentHealthData(
    Guid ProjectId,
    string ProjectName,
    Guid EnvironmentId,
    string EnvironmentName,
    string EnvironmentKind,
    HealthCheckData? Checked,
    DateTimeOffset? HealthySince,
    DateTimeOffset? FailingSince,
    int ConsecutiveFailures,
    DateTimeOffset? LastHealthyAt,
    UptimeReading? Uptime,
    int FailedChecksInWindow,
    int? LongestOutageSeconds);

/// <param name="Dependencies">
/// What the application reported about the things it depends on. Empty when it reported none, which is not the
/// same as everything being fine.
/// </param>
public sealed record HealthCheckData(
    ApplicationHealthState State,
    DateTimeOffset ObservedAtUtc,
    double? ResponseMilliseconds,
    IReadOnlyList<DependencyHealthData> Dependencies);

public sealed record DependencyHealthData(string Name, ApplicationHealthState State);

/// <param name="Type">
/// The recorded transition, such as <c>healthFailed</c>. Recorded when it happened, never re-derived from the
/// current state - which is the only way a change can be reported honestly after the fact.
/// </param>
public sealed record HealthTransitionData(
    DateTimeOffset OccurredAtUtc,
    string ProjectName,
    string EnvironmentName,
    MonitoringActivityType Type);
