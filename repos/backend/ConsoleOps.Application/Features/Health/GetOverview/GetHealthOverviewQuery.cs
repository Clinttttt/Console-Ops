using ConsoleOps.Application.Abstractions.Messaging;
using ConsoleOps.Domain.Monitoring;
using MediatR;

namespace ConsoleOps.Application.Features.Health.GetOverview;

public sealed record GetHealthOverviewQuery : IRequest<Result<HealthOverviewResponse>>;

/// <param name="Summary">Counts an operator reads first: how many are fine, how many are not, how fresh that is.</param>
/// <param name="StateChanges">Transitions as they were recorded, newest first.</param>
public sealed record HealthOverviewResponse(
    DateTimeOffset ObservedAt,
    HealthSummaryResponse Summary,
    IReadOnlyList<EnvironmentHealthResponse> Environments,
    IReadOnlyList<HealthStateChangeResponse> StateChanges);

/// <param name="LastCheckedAt">
/// The most recent check across every environment, or <c>null</c> when nothing has been checked at all.
/// </param>
public sealed record HealthSummaryResponse(
    int Healthy,
    int Degraded,
    int Down,
    DateTimeOffset? LastCheckedAt);

/// <param name="State">
/// The seven states the rest of Console Ops uses, camel case. <c>unknown</c> means no check exists, never
/// "probably fine", and <c>running</c> is not promoted to <c>healthy</c>.
/// </param>
/// <param name="Window">The last 24 hours of recorded checks for this environment.</param>
public sealed record EnvironmentHealthResponse(
    string Id,
    Guid ProjectId,
    string ProjectName,
    string EnvironmentName,
    string EnvironmentKind,
    string State,
    DateTimeOffset? CheckedAt,
    double? ResponseMilliseconds,
    IReadOnlyList<HealthCheckResponse> Checks,
    DateTimeOffset? HealthySince,
    DateTimeOffset? FailingSince,
    int ConsecutiveFailures,
    DateTimeOffset? LastHealthyAt,
    HealthWindowResponse Window);

/// <param name="Kind">
/// What the check covers: <c>application</c> for the endpoint itself, or the kind inferred from a dependency's
/// name so the screen can group them. <c>unknown</c> when the name says nothing recognizable.
/// </param>
public sealed record HealthCheckResponse(
    string Name,
    string Kind,
    string State,
    double? ResponseMilliseconds);

/// <param name="AvailabilityPercentage">
/// <c>null</c> below the minimum number of checks. A percentage from a handful of checks would be a guess
/// dressed as a measurement.
/// </param>
public sealed record HealthWindowResponse(
    double? AvailabilityPercentage,
    int? Checks,
    int? FailedChecks,
    int? LongestOutageSeconds);

/// <summary>
/// Reports what is functioning right now across every monitored environment.
/// <para>
/// Every value is a recorded observation. The screen this answers has to distinguish an application that is
/// failing from a dependency that is, an environment nobody has checked from one that is fine, and a window with
/// too few checks from one that is perfect - so none of those are collapsed here.
/// </para>
/// </summary>
public sealed class GetHealthOverviewQueryHandler(
    IHealthOverviewReadStore readStore,
    TimeProvider timeProvider)
    : IRequestHandler<GetHealthOverviewQuery, Result<HealthOverviewResponse>>
{
    internal const int WindowHours = 24;
    internal const int TransitionCount = 8;

    public async Task<Result<HealthOverviewResponse>> Handle(
        GetHealthOverviewQuery request,
        CancellationToken cancellationToken)
    {
        HealthOverviewData data = await readStore.ReadAsync(WindowHours, TransitionCount, cancellationToken);

        EnvironmentHealthResponse[] environments = data.Environments
            .Select(ToEnvironmentResponse)
            .OrderBy(environment => environment.ProjectName, StringComparer.OrdinalIgnoreCase)
            .ThenBy(environment => environment.EnvironmentName, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        return Result<HealthOverviewResponse>.Success(new HealthOverviewResponse(
            timeProvider.GetUtcNow(),
            Summarize(environments),
            environments,
            [.. data.Transitions.Select(ToStateChangeResponse)]));
    }

    /// <summary>
    /// Counts by what an operator would act on. Running and healthy are both fine to leave alone; an environment
    /// nobody has checked is counted in neither column, because it is not evidence of anything.
    /// </summary>
    private static HealthSummaryResponse Summarize(IReadOnlyList<EnvironmentHealthResponse> environments) =>
        new(
            environments.Count(environment => environment.State is "healthy" or "running"),
            environments.Count(environment => environment.State is "degraded"),
            environments.Count(environment => environment.State is "unhealthy" or "unreachable"),
            environments
                .Select(environment => environment.CheckedAt)
                .Where(checkedAt => checkedAt is not null)
                .DefaultIfEmpty(null)
                .Max());

    private static EnvironmentHealthResponse ToEnvironmentResponse(EnvironmentHealthData data)
    {
        HealthCheckData? latest = data.Checked;

        return new EnvironmentHealthResponse(
            // Stable per environment, which is what the screen selects on.
            data.EnvironmentId.ToString(),
            data.ProjectId,
            data.ProjectName,
            data.EnvironmentName,
            data.EnvironmentKind,
            latest is null ? "unknown" : ToCamelCase(latest.State),
            latest?.ObservedAtUtc,
            latest?.ResponseMilliseconds,
            ToChecks(latest),
            data.HealthySince,
            data.FailingSince,
            data.ConsecutiveFailures,
            data.LastHealthyAt,
            new HealthWindowResponse(
                data.Uptime?.Percentage,
                data.Uptime?.Checks,
                data.Uptime is null ? null : data.FailedChecksInWindow,
                data.LongestOutageSeconds));
    }

    /// <summary>
    /// The application's own check first, then each dependency it reported. The application leads because a
    /// dependency failing while it answers is the case the screen exists to make visible.
    /// </summary>
    private static HealthCheckResponse[] ToChecks(HealthCheckData? latest)
    {
        if (latest is null)
        {
            return [];
        }

        return
        [
            new HealthCheckResponse(
                "Application",
                "application",
                ToCamelCase(latest.State),
                latest.ResponseMilliseconds),
            .. latest.Dependencies.Select(dependency => new HealthCheckResponse(
                dependency.Name,
                KindOf(dependency.Name),
                ToCamelCase(dependency.State),
                // A dependency check reports a state; the application reports the round trip.
                null))
        ];
    }

    /// <summary>
    /// The kind inferred from the name the application chose, so the screen can group checks. It is a display
    /// hint and nothing depends on it being right: an unrecognized name is <c>unknown</c> rather than guessed.
    /// </summary>
    private static string KindOf(string name)
    {
        string lowered = name.ToLowerInvariant();

        if (lowered.Contains("redis") || lowered.Contains("cache") || lowered.Contains("memcach"))
        {
            return "cache";
        }

        if (lowered.Contains("db") || lowered.Contains("database") || lowered.Contains("sql")
            || lowered.Contains("postgres") || lowered.Contains("mongo"))
        {
            return "database";
        }

        return lowered.Contains("http") || lowered.Contains("api") || lowered.Contains("service")
            ? "external"
            : "unknown";
    }

    private static HealthStateChangeResponse ToStateChangeResponse(HealthTransitionData transition) =>
        new(
            transition.OccurredAtUtc,
            transition.ProjectName,
            transition.EnvironmentName,
            Describe(transition.Type),
            transition.Type is MonitoringActivityType.HealthRecovered ? "healthy" : "down");

    /// <summary>Operational wording for a recorded transition. The screen never invents one of its own.</summary>
    private static string Describe(MonitoringActivityType type) => type switch
    {
        MonitoringActivityType.HealthFailed => "Health failed",
        MonitoringActivityType.HealthRecovered => "Health recovered",
        _ => type.ToString()
    };

    private static string ToCamelCase<TEnum>(TEnum value)
        where TEnum : struct, Enum
    {
        string text = value.ToString();
        return char.ToLowerInvariant(text[0]) + text[1..];
    }
}

/// <param name="Level">
/// The tone the change should read as: a recovery is healthy, a failure is down. Derived from the recorded type
/// rather than from the environment's current state, which may have changed since.
/// </param>
public sealed record HealthStateChangeResponse(
    DateTimeOffset OccurredAt,
    string ProjectName,
    string EnvironmentName,
    string Description,
    string Level);
