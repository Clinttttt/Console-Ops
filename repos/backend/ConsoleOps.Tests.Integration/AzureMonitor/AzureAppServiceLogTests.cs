using ConsoleOps.Application.Integrations.AzureMonitor;
using ConsoleOps.Infrastructure.Integrations.AzureMonitor;

namespace ConsoleOps.Tests.Integration.AzureMonitor;

/// <summary>
/// Reading App Service console output.
/// </summary>
/// <remarks>
/// The payloads here are the shapes actually observed in a live workspace, not invented ones. The distinction the
/// whole reader exists to make is that the table's own <c>Level</c> column says <c>Informational</c> on every row,
/// including the warnings - so a reader that trusted it would be wrong and would look right.
/// </remarks>
public sealed class AzureAppServiceLogTests
{
    private static readonly DateTimeOffset Emitted = new(2026, 8, 27, 15, 15, 32, TimeSpan.Zero);

    [Fact]
    public void Query_asks_only_for_the_site_it_was_given()
    {
        string kql = AzureAppServiceLogQuery.Build("stalltrack-api-cly-2026", 100, null);

        Assert.Contains(AzureAppServiceLogQuery.TableName, kql, StringComparison.Ordinal);
        // Matched on the resource path, because this table carries no site name column. The leading separator
        // keeps a site called "api" from matching one called "public-api".
        Assert.Contains("\"/sites/stalltrack-api-cly-2026\"", kql, StringComparison.Ordinal);
        Assert.Contains("take 100", kql, StringComparison.Ordinal);
        // The line, not the platform's description of the stream.
        Assert.Contains("Message = ResultDescription", kql, StringComparison.Ordinal);
        Assert.DoesNotContain("Level", kql, StringComparison.Ordinal);
    }

    [Fact]
    public void Query_sends_operator_text_as_a_literal()
    {
        string kql = AzureAppServiceLogQuery.Build("site", 10, "\" | project 1 //");

        // The injected quote is escaped, so the literal is never closed early and what follows stays data. Testing
        // for the absence of the text would pass for the wrong reason: it is present, and harmless, inside quotes.
        Assert.Contains("contains \"\\\" | project 1 //\"", kql, StringComparison.Ordinal);
    }

    /// <summary>The severity is inside the line, and it is declared rather than inferred.</summary>
    [Fact]
    public void Reads_the_level_and_category_the_application_declared()
    {
        AzureAppServiceLogRow row = Row(
            """
            {"Timestamp":"2026-08-27T15:15:32.297Z","EventId":20101,"LogLevel":"Warning",
             "Category":"Microsoft.EntityFrameworkCore.Database.Command","Message":"Executed DbCommand (2ms)"}
            """);

        ApplicationLogEntry entry = Assert.Single(AzureAppServiceLogNormalizer.Normalize([row]));

        Assert.Equal(ApplicationLogLevel.Warning, entry.Level);
        Assert.False(entry.LevelIsDerived);
        Assert.Equal("Microsoft.EntityFrameworkCore.Database.Command", entry.Category);
        Assert.Equal("Executed DbCommand (2ms)", entry.Message);
    }

    /// <summary>32 of 206 verified rows were the platform's own startup text, with no level to read.</summary>
    [Fact]
    public void Keeps_a_plain_text_line_as_text_without_inventing_a_level()
    {
        AzureAppServiceLogRow row = Row("Pulling image: mcr.microsoft.com/appsvc/staticsite:latest");

        ApplicationLogEntry entry = Assert.Single(AzureAppServiceLogNormalizer.Normalize([row]));

        Assert.Equal(ApplicationLogLevel.Unknown, entry.Level);
        Assert.False(entry.LevelIsDerived);
        Assert.Null(entry.Category);
        Assert.Equal("Pulling image: mcr.microsoft.com/appsvc/staticsite:latest", entry.Message);
    }

    /// <summary>
    /// A serialized payload printed to the console is not a log record. Treating it as one would attach a
    /// severity nobody stated.
    /// </summary>
    [Fact]
    public void Does_not_promote_arbitrary_json_to_a_log_record()
    {
        AzureAppServiceLogRow row = Row("""{"stallId":"59a4523e","fishKilos":12}""");

        ApplicationLogEntry entry = Assert.Single(AzureAppServiceLogNormalizer.Normalize([row]));

        Assert.Equal(ApplicationLogLevel.Unknown, entry.Level);
        Assert.Null(entry.Category);
        Assert.Contains("stallId", entry.Message, StringComparison.Ordinal);
    }

    /// <summary>
    /// This table does not separate the two streams, and the reader says so rather than choosing one. Nor does an
    /// App Service row carry a revision.
    /// </summary>
    [Fact]
    public void Claims_neither_a_stream_nor_a_revision_it_cannot_know()
    {
        ApplicationLogEntry entry = Assert.Single(
            AzureAppServiceLogNormalizer.Normalize([Row("""{"LogLevel":"Error","Message":"boom"}""")]));

        Assert.Equal(ApplicationLogStream.Unknown, entry.Stream);
        Assert.Null(entry.Revision);
        Assert.Equal("lw0mdlwk0002Y1", entry.Replica);
    }

    [Fact]
    public void Gives_two_identical_lines_in_one_batch_different_identities()
    {
        AzureAppServiceLogRow row = Row("""{"LogLevel":"Information","Message":"same"}""");

        IReadOnlyList<ApplicationLogEntry> entries = AzureAppServiceLogNormalizer.Normalize([row, row]);

        Assert.Equal(2, entries.Count);
        // A shared timestamp must not collapse two lines into one id, or the screen loses a selection when it pages.
        Assert.NotEqual(entries[0].Id, entries[1].Id);
    }

    [Fact]
    public void Reads_an_exception_only_when_the_line_carried_one()
    {
        ApplicationLogEntry withException = Assert.Single(
            AzureAppServiceLogNormalizer.Normalize(
                [Row("""{"LogLevel":"Error","Message":"failed","Exception":"System.Exception: nope"}""")]));
        ApplicationLogEntry without = Assert.Single(
            AzureAppServiceLogNormalizer.Normalize([Row("""{"LogLevel":"Error","Message":"failed"}""")]));

        Assert.Equal("System.Exception: nope", withException.StackTrace);
        Assert.Null(without.StackTrace);
    }

    private static AzureAppServiceLogRow Row(string message) =>
        new(Emitted, Emitted, message, "lw0mdlwk0002Y1");
}
