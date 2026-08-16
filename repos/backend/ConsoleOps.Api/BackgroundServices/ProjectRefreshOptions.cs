namespace ConsoleOps.Api.BackgroundServices;

/// <summary>
/// How often Console Ops refreshes projects on its own.
/// <para>
/// Bounds are enforced rather than trusted. Too short a period would hammer the operator's own
/// applications and burn the GitHub rate limit for no extra insight; an unbounded value would make the
/// screens quietly stale.
/// </para>
/// </summary>
public sealed class ProjectRefreshOptions
{
    public const string SectionName = "Monitoring:Refresh";

    internal const int MinimumIntervalSeconds = 30;
    internal const int MaximumIntervalSeconds = 3600;
    internal const int DefaultIntervalSeconds = 300;
    internal const int DefaultStartupDelaySeconds = 10;
    internal const int DefaultProjectSpacingMilliseconds = 500;

    /// <summary>Set to <c>false</c> to collect only when the operator asks.</summary>
    public bool Enabled { get; set; } = true;

    public int IntervalSeconds { get; set; } = DefaultIntervalSeconds;

    /// <summary>Delay before the first sweep, so startup is not competing with the first page load.</summary>
    public int StartupDelaySeconds { get; set; } = DefaultStartupDelaySeconds;

    /// <summary>
    /// Pause between projects in one sweep. Refreshes are already serialized per project; this keeps a
    /// sweep from arriving at GitHub as a burst.
    /// </summary>
    public int ProjectSpacingMilliseconds { get; set; } = DefaultProjectSpacingMilliseconds;

    public TimeSpan Interval => TimeSpan.FromSeconds(
        Math.Clamp(IntervalSeconds, MinimumIntervalSeconds, MaximumIntervalSeconds));

    public TimeSpan StartupDelay => TimeSpan.FromSeconds(Math.Clamp(StartupDelaySeconds, 0, 300));

    public TimeSpan ProjectSpacing => TimeSpan.FromMilliseconds(
        Math.Clamp(ProjectSpacingMilliseconds, 0, 10_000));
}
