namespace ConsoleOps.Application.Integrations.ApplicationMonitoring;

public sealed record ApplicationProbeTarget(
    string? HealthUrl,
    string? VersionUrl);

public sealed record ApplicationProbeResult(
    ApplicationHealthObservation Health,
    ApplicationVersionObservation Version);

public sealed record ApplicationHealthObservation(
    ApplicationHealthState State,
    TimeSpan? ResponseDuration,
    DateTimeOffset ObservedAtUtc,
    IReadOnlyList<DependencyHealthObservation> Dependencies);

public sealed record DependencyHealthObservation(
    string Name,
    ApplicationHealthState State);

public enum ApplicationHealthState
{
    Healthy,
    Degraded,
    Unhealthy,
    Unreachable,
    Unknown,
    NotConfigured
}

public sealed record ApplicationVersionObservation(
    ApplicationVersionState State,
    string? Application,
    string? Version,
    string? CommitSha,
    string? Environment,
    DateTimeOffset? BuiltAtUtc,
    DateTimeOffset ObservedAtUtc);

public enum ApplicationVersionState
{
    Available,
    Unknown,
    NotConfigured
}
