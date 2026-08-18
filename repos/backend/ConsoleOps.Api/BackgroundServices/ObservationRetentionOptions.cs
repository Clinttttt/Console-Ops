namespace ConsoleOps.Api.BackgroundServices;

/// <summary>
/// How long Console Ops keeps what it recorded.
/// <para>
/// Every sweep appends observations, so without a window the tables grow for as long as the instance runs. The
/// screens read bounded windows - 24 hours of health, a page of releases - so old rows cost storage without
/// answering anything.
/// </para>
/// </summary>
public sealed class ObservationRetentionOptions
{
    public const string SectionName = "Monitoring:Retention";

    internal const int DefaultDays = 30;
    internal const int DefaultIntervalHours = 6;
    internal const int DefaultBatchSize = 5_000;
    internal const int DefaultStartupDelaySeconds = 120;

    /// <summary>
    /// Off means nothing is ever deleted, which is a deliberate choice a deployment may make.
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// How many days of observations to keep. Clamped to at least a week: a shorter window would delete history
    /// the availability figures and release verification still read.
    /// </summary>
    public int Days { get; set; } = DefaultDays;

    public int IntervalHours { get; set; } = DefaultIntervalHours;

    /// <summary>
    /// Rows deleted per statement. Bounded so a first run against a long-neglected database cannot hold a lock
    /// long enough to stall collection.
    /// </summary>
    public int BatchSize { get; set; } = DefaultBatchSize;

    public int StartupDelaySeconds { get; set; } = DefaultStartupDelaySeconds;

    internal TimeSpan Window => TimeSpan.FromDays(Math.Clamp(Days, 7, 3_650));

    internal TimeSpan Interval => TimeSpan.FromHours(Math.Clamp(IntervalHours, 1, 168));

    internal int Batch => Math.Clamp(BatchSize, 100, 50_000);

    internal TimeSpan StartupDelay =>
        TimeSpan.FromSeconds(Math.Clamp(StartupDelaySeconds, 0, 3_600));
}
