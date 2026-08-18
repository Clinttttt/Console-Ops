using ConsoleOps.Domain.Monitoring;

namespace ConsoleOps.Tests.Unit.Domain;

public sealed class UptimeTests
{
    private static readonly DateTimeOffset WindowStart = new(2026, 8, 15, 0, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Calculate_WithTooFewChecks_ReportsNothing()
    {
        UptimeSample[] samples = Acceptable(Uptime.MinimumChecks - 1);

        Assert.Null(Uptime.Calculate(samples, WindowStart));
    }

    [Fact]
    public void Calculate_WithEnoughChecks_ReportsTheObservedShare()
    {
        List<UptimeSample> samples = [.. Acceptable(18)];
        samples.AddRange(Failing(2, startMinute: 18));

        UptimeReading reading = Assert.IsType<UptimeReading>(
            Uptime.Calculate(samples, WindowStart));

        Assert.Equal(90d, reading.Percentage);
        Assert.Equal(20, reading.Checks);
        Assert.Equal(WindowStart, reading.SinceUtc);
    }

    [Fact]
    public void Calculate_RoundsToOneDecimalRatherThanFlatteringTheFigure()
    {
        List<UptimeSample> samples = [.. Acceptable(1000)];
        samples.AddRange(Failing(1, startMinute: 1000));

        UptimeReading reading = Assert.IsType<UptimeReading>(
            Uptime.Calculate(samples, WindowStart));

        // 1000/1001 is 99.9001%, which must not be shown as 100%.
        Assert.Equal(99.9d, reading.Percentage);
    }

    [Fact]
    public void Calculate_IgnoresChecksThatEstablishedNothing()
    {
        List<UptimeSample> samples = [.. Acceptable(12)];
        // An unknown state, or an environment with no health endpoint, is not evidence of being up.
        samples.AddRange(Enumerable.Range(0, 50).Select(index => new UptimeSample(
            MonitoringCondition.Indeterminate,
            WindowStart.AddMinutes(100 + index))));

        UptimeReading reading = Assert.IsType<UptimeReading>(
            Uptime.Calculate(samples, WindowStart));

        Assert.Equal(12, reading.Checks);
        Assert.Equal(100d, reading.Percentage);
    }

    [Fact]
    public void Calculate_ExcludesChecksFromBeforeTheWindow()
    {
        List<UptimeSample> samples = [.. Acceptable(12)];
        samples.AddRange(Enumerable.Range(0, 40).Select(index => new UptimeSample(
            MonitoringCondition.Failure,
            WindowStart.AddMinutes(-index - 1))));

        UptimeReading reading = Assert.IsType<UptimeReading>(
            Uptime.Calculate(samples, WindowStart));

        Assert.Equal(12, reading.Checks);
        Assert.Equal(100d, reading.Percentage);
    }

    [Fact]
    public void Calculate_SamplesOnlyHoursThatContainChecks()
    {
        List<UptimeSample> samples = [];
        // First hour: fully available.
        samples.AddRange(Enumerable.Range(0, 6).Select(index => new UptimeSample(
            MonitoringCondition.Acceptable,
            WindowStart.AddMinutes(index * 5))));
        // Second hour: nothing recorded at all.
        // Third hour: half the checks failed.
        samples.AddRange(Enumerable.Range(0, 3).Select(index => new UptimeSample(
            MonitoringCondition.Acceptable,
            WindowStart.AddHours(2).AddMinutes(index * 5))));
        samples.AddRange(Enumerable.Range(0, 3).Select(index => new UptimeSample(
            MonitoringCondition.Failure,
            WindowStart.AddHours(2).AddMinutes(30 + index * 5))));

        UptimeReading reading = Assert.IsType<UptimeReading>(
            Uptime.Calculate(samples, WindowStart));

        // Two samples, oldest first. The empty hour is absent rather than drawn as zero or as full.
        Assert.Equal([100d, 50d], reading.HourlySamples);
        Assert.Equal(75d, reading.Percentage);
    }

    private static UptimeSample[] Acceptable(int count) =>
        Enumerable.Range(0, count)
            .Select(index => new UptimeSample(
                MonitoringCondition.Acceptable,
                WindowStart.AddMinutes(index)))
            .ToArray();

    private static UptimeSample[] Failing(int count, int startMinute) =>
        Enumerable.Range(0, count)
            .Select(index => new UptimeSample(
                MonitoringCondition.Failure,
                WindowStart.AddMinutes(startMinute + index)))
            .ToArray();
}
