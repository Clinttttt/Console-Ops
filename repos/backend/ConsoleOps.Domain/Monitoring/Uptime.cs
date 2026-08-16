namespace ConsoleOps.Domain.Monitoring;

/// <summary>One health check, reduced to whether the application was answering acceptably.</summary>
public sealed record UptimeSample(MonitoringCondition Condition, DateTimeOffset ObservedAtUtc);

/// <summary>
/// Observed availability over a window.
/// </summary>
/// <param name="Percentage">
/// Share of measured checks that were acceptable, to one decimal. This is availability as sampled by
/// Console Ops, not a provider's uptime guarantee.
/// </param>
/// <param name="Checks">How many measured checks the figure rests on. Reported so it can be shown.</param>
/// <param name="HourlySamples">
/// Availability per UTC hour, oldest first, for hours that actually contain checks. Hours with no check
/// are omitted rather than drawn as zero or as full availability.
/// </param>
public sealed record UptimeReading(
    double Percentage,
    int Checks,
    DateTimeOffset SinceUtc,
    IReadOnlyList<double> HourlySamples);

/// <summary>
/// Availability calculated from recorded health checks.
/// <para>
/// Only checks that established something count: an indeterminate check (unknown state, or no health
/// endpoint configured) is not evidence of being up or down, so it is excluded from both sides of the
/// ratio rather than being charitably counted as available.
/// </para>
/// <para>
/// Below <see cref="MinimumChecks"/> the window has no figure at all. Three checks in a day can produce
/// a confident-looking 100%, which would be the most misleading number on the screen.
/// </para>
/// </summary>
public static class Uptime
{
    /// <summary>
    /// Fewest measured checks that may produce a figure. At the default collection interval this is
    /// about an hour of observation.
    /// </summary>
    public const int MinimumChecks = 12;

    public static UptimeReading? Calculate(
        IReadOnlyCollection<UptimeSample> samples,
        DateTimeOffset sinceUtc)
    {
        ArgumentNullException.ThrowIfNull(samples);

        UptimeSample[] measured = samples
            .Where(sample => sample.ObservedAtUtc >= sinceUtc
                && sample.Condition is MonitoringCondition.Acceptable or MonitoringCondition.Failure)
            .ToArray();
        if (measured.Length < MinimumChecks)
        {
            return null;
        }

        int acceptable = measured.Count(sample =>
            sample.Condition == MonitoringCondition.Acceptable);
        double[] hourlySamples = measured
            .GroupBy(sample => new DateTimeOffset(
                sample.ObservedAtUtc.UtcDateTime.Date.AddHours(sample.ObservedAtUtc.UtcDateTime.Hour),
                TimeSpan.Zero))
            .OrderBy(group => group.Key)
            .Select(group => ToPercentage(
                group.Count(sample => sample.Condition == MonitoringCondition.Acceptable),
                group.Count()))
            .ToArray();

        return new UptimeReading(
            ToPercentage(acceptable, measured.Length),
            measured.Length,
            sinceUtc,
            hourlySamples);
    }

    private static double ToPercentage(int acceptable, int measured) =>
        Math.Round(acceptable / (double)measured * 100, 1);
}
