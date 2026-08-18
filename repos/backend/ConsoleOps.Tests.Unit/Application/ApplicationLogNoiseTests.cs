using ConsoleOps.Application.Integrations.AzureMonitor;

namespace ConsoleOps.Tests.Unit.Application;

/// <summary>
/// What may be left out of a readable stream, and what may never be.
/// <para>
/// Hiding lines is the one feature on this screen that can make Console Ops appear to have seen less than it
/// did, so the rules are pinned here rather than left to the category list.
/// </para>
/// </summary>
public sealed class ApplicationLogNoiseTests
{
    [Theory]
    [InlineData("Microsoft.EntityFrameworkCore.Database.Command")]
    [InlineData("Microsoft.EntityFrameworkCore.Database.Connection")]
    [InlineData("Microsoft.EntityFrameworkCore.Query")]
    [InlineData("System.Net.Http.HttpClient.IGitHubProjectReader.LogicalHandler")]
    [InlineData("Microsoft.AspNetCore.Routing.EndpointMiddleware")]
    public void Noise_IsFrameworkChatterAtInformationOrBelow(string category)
    {
        Assert.True(ApplicationLogNoise.IsNoise(Entry(category, ApplicationLogLevel.Information)));
        Assert.True(ApplicationLogNoise.IsNoise(Entry(category, ApplicationLogLevel.Debug)));
    }

    [Theory]
    [InlineData(ApplicationLogLevel.Warning)]
    [InlineData(ApplicationLogLevel.Error)]
    [InlineData(ApplicationLogLevel.Critical)]
    public void Noise_NeverHidesAWarningOrWorse(ApplicationLogLevel level)
    {
        // A failed database command is exactly what an operator opened the screen for.
        Assert.False(
            ApplicationLogNoise.IsNoise(Entry("Microsoft.EntityFrameworkCore.Database.Command", level)));
    }

    [Fact]
    public void Noise_NeverHidesALineWhoseCategoryIsUnknown()
    {
        // An unparsed console line is an unknown, and an unknown is not assumed to be unimportant.
        Assert.False(ApplicationLogNoise.IsNoise(Entry(null, ApplicationLogLevel.Unknown)));
        Assert.False(ApplicationLogNoise.IsNoise(Entry(null, ApplicationLogLevel.Information)));
    }

    [Theory]
    [InlineData("Spinner.Orders")]
    [InlineData("Spinner.Payments")]
    [InlineData("Microsoft.Hosting.Lifetime")]
    [InlineData("Microsoft.AspNetCore.Hosting.Diagnostics")]
    public void Noise_KeepsTheApplicationsOwnLinesAndTheOnesThatCarrySignal(string category)
    {
        // Hosting lifetime says the app started; the request logger carries the status code and duration.
        Assert.False(ApplicationLogNoise.IsNoise(Entry(category, ApplicationLogLevel.Information)));
    }

    private static ApplicationLogEntry Entry(string? category, ApplicationLogLevel level) => new(
        "id",
        DateTimeOffset.UnixEpoch,
        DateTimeOffset.UnixEpoch,
        level,
        true,
        category,
        "message",
        null,
        ApplicationLogStream.Stdout,
        null,
        null);
}
