using ConsoleOps.Application.Integrations.AzureMonitor;
using ConsoleOps.Infrastructure.Integrations.AzureMonitor;

namespace ConsoleOps.Tests.Integration.AzureMonitor;

/// <summary>
/// Container console output is text. These tests pin what may be recovered from it and, just as
/// importantly, what must stay unknown.
/// </summary>
public sealed class AzureConsoleLogNormalizerTests
{
    private static readonly DateTimeOffset At = new(2026, 8, 15, 23, 52, 14, TimeSpan.Zero);

    [Fact]
    public void Normalize_ReadsTheConsoleLoggerConvention()
    {
        // Newest first, as the provider returns rows.
        AzureConsoleLogRow[] rows =
        [
            Row(At.AddSeconds(1), "      Charge authorized by Stripe"),
            Row(At, "info: Spinner.Payments[0]")
        ];

        ApplicationLogEntry entry = Assert.Single(AzureConsoleLogNormalizer.Normalize(rows));

        Assert.Equal(ApplicationLogLevel.Information, entry.Level);
        Assert.Equal("Spinner.Payments", entry.Category);
        Assert.Equal("Charge authorized by Stripe", entry.Message);
        Assert.Equal(At, entry.OccurredAtUtc);
        // The level came from a text convention, not from the emitter declaring it.
        Assert.True(entry.LevelIsDerived);
        Assert.Null(entry.StackTrace);
    }

    [Fact]
    public void Normalize_DoesNotMistakeAMessageForACategory()
    {
        // Without a bracketed event id there is no category, and the text is the message.
        AzureConsoleLogRow[] rows = [Row(At, "warn: Provider request required a retry")];

        ApplicationLogEntry entry = Assert.Single(AzureConsoleLogNormalizer.Normalize(rows));

        Assert.Equal(ApplicationLogLevel.Warning, entry.Level);
        Assert.Null(entry.Category);
        Assert.Equal("Provider request required a retry", entry.Message);
    }

    [Fact]
    public void Normalize_FoldsAStackTraceIntoTheEntryItBelongsTo()
    {
        AzureConsoleLogRow[] rows =
        [
            Row(At.AddSeconds(2), "         at Spinner.Orders.CheckoutHandler.Handle()"),
            Row(At.AddSeconds(1), "      at Spinner.Payments.ProviderClient.ChargeAsync()"),
            Row(At, "fail: Spinner.Payments[0]"),
        ];

        ApplicationLogEntry entry = Assert.Single(AzureConsoleLogNormalizer.Normalize(rows));

        Assert.Equal(ApplicationLogLevel.Error, entry.Level);
        Assert.NotNull(entry.StackTrace);
        Assert.Contains("ProviderClient.ChargeAsync", entry.StackTrace);
        Assert.Contains("CheckoutHandler.Handle", entry.StackTrace);
        // The stream never has to render a stack trace, so it is not part of the message.
        Assert.DoesNotContain("at Spinner", entry.Message);
    }

    [Fact]
    public void Normalize_LeavesAPlainLineUnknownRatherThanCallingItInformation()
    {
        AzureConsoleLogRow[] rows = [Row(At, "Now listening on: http://[::]:8080")];

        ApplicationLogEntry entry = Assert.Single(AzureConsoleLogNormalizer.Normalize(rows));

        Assert.Equal(ApplicationLogLevel.Unknown, entry.Level);
        Assert.False(entry.LevelIsDerived);
        Assert.Null(entry.Category);
        Assert.Equal("Now listening on: http://[::]:8080", entry.Message);
    }

    [Fact]
    public void Normalize_TreatsUnprefixedStandardErrorAsADerivedError()
    {
        AzureConsoleLogRow[] rows = [Row(At, "Unhandled exception. System.Exception: boom", stream: "stderr")];

        ApplicationLogEntry entry = Assert.Single(AzureConsoleLogNormalizer.Normalize(rows));

        Assert.Equal(ApplicationLogLevel.Error, entry.Level);
        Assert.True(entry.LevelIsDerived);
        Assert.Equal(ApplicationLogStream.Stderr, entry.Stream);
    }

    [Fact]
    public void Normalize_ReturnsNewestFirstAndCarriesRuntimeIdentity()
    {
        AzureConsoleLogRow[] rows =
        [
            Row(At.AddSeconds(2), "info: Second[0] later"),
            Row(At, "info: First[0] earlier"),
        ];

        IReadOnlyList<ApplicationLogEntry> entries = AzureConsoleLogNormalizer.Normalize(rows);

        Assert.Equal(2, entries.Count);
        Assert.Equal(At.AddSeconds(2), entries[0].OccurredAtUtc);
        Assert.Equal(At, entries[1].OccurredAtUtc);
        Assert.All(entries, entry => Assert.Equal("spinner-api--000021", entry.Revision));
        Assert.All(entries, entry => Assert.Equal("spinner-api-7d8c9f6b5c-xk2pz", entry.Replica));
    }

    [Fact]
    public void Normalize_GivesEveryEntryAStableDistinctId()
    {
        // Two identical lines at the same instant: the provider has no row id, so ours must still differ.
        AzureConsoleLogRow[] rows =
        [
            Row(At, "info: Spinner.Orders[0] repeated"),
            Row(At, "info: Spinner.Orders[0] repeated"),
        ];

        IReadOnlyList<ApplicationLogEntry> first = AzureConsoleLogNormalizer.Normalize(rows);
        IReadOnlyList<ApplicationLogEntry> second = AzureConsoleLogNormalizer.Normalize(rows);

        Assert.Equal(2, first.Count);
        Assert.NotEqual(first[0].Id, first[1].Id);
        // Deterministic, so a selection survives a poll that returns the same window.
        Assert.Equal(first.Select(entry => entry.Id), second.Select(entry => entry.Id));
    }

    [Fact]
    public void Normalize_WithNoRows_ReturnsNothing() =>
        Assert.Empty(AzureConsoleLogNormalizer.Normalize([]));

    private static AzureConsoleLogRow Row(
        DateTimeOffset occurredAt,
        string log,
        string stream = "stdout") =>
        new(
            occurredAt,
            // Ingestion time is shared across a batch, which is why it never drives ordering.
            occurredAt.AddSeconds(1),
            log,
            stream,
            "spinner-api--000021",
            "spinner-api-7d8c9f6b5c-xk2pz");
}
