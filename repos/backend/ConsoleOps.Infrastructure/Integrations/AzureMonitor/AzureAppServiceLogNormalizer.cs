using System.Text.Json;
using ConsoleOps.Application.Integrations.AzureMonitor;

namespace ConsoleOps.Infrastructure.Integrations.AzureMonitor;

/// <summary>
/// One row of <c>AppServiceConsoleLogs</c>, as projected by <see cref="AzureAppServiceLogQuery"/>.
/// </summary>
/// <param name="Message">
/// The console line exactly as App Service recorded it. Structured or not - deciding which is this
/// normalizer's job, not the query's.
/// </param>
internal sealed record AzureAppServiceLogRow(
    DateTimeOffset? EmittedAt,
    DateTimeOffset? ReceivedAt,
    string? Message,
    string? Host);

/// <summary>
/// Turns App Service console rows into log entries.
/// <para>
/// Unlike a container app's console output, these lines are not folded: an application that logs structured JSON
/// emits one complete record per line, so there is no prefix to parse and no continuation to attach. What the
/// line carries instead is the application's own <c>LogLevel</c> and <c>Category</c>, which is better evidence
/// than anything a console convention could give - so severity here is <em>declared</em>, and
/// <c>LevelIsDerived</c> is false.
/// </para>
/// <para>
/// Not every line is structured. Of the rows this was verified against, 174 of 206 parsed and 32 did not: those
/// were the platform's own container startup lines. An unparsed line keeps <see cref="ApplicationLogLevel.Unknown"/>
/// and no category rather than being assigned a plausible one, and because it has no category it cannot be
/// excluded as framework noise either - nothing attributes it, so nothing may claim it is chatter.
/// </para>
/// </summary>
internal static class AzureAppServiceLogNormalizer
{
    private const int MaximumMessageLength = 8_000;
    private const int MaximumStackTraceLength = 16_000;

    public static IReadOnlyList<ApplicationLogEntry> Normalize(IReadOnlyList<AzureAppServiceLogRow> rows)
    {
        ArgumentNullException.ThrowIfNull(rows);

        List<ApplicationLogEntry> entries = new(rows.Count);
        DateTimeOffset? previousTimestamp = null;
        int ordinal = 0;

        foreach (AzureAppServiceLogRow row in rows)
        {
            string text = (row.Message ?? string.Empty).TrimEnd();
            if (text.Length == 0)
            {
                continue;
            }

            DateTimeOffset occurredAt = row.EmittedAt ?? row.ReceivedAt ?? default;

            // Rows sharing a timestamp need distinguishing, or two identical lines in one batch collapse to one
            // id and the screen loses a selection when it pages.
            ordinal = occurredAt == previousTimestamp ? ordinal + 1 : 0;
            previousTimestamp = occurredAt;

            StructuredLine? structured = ReadStructuredLine(text);
            string message = Truncate(structured?.Message ?? text, MaximumMessageLength);

            entries.Add(new ApplicationLogEntry(
                AzureConsoleLogNormalizer.ComposeId(
                    occurredAt,
                    ordinal,
                    row.Host,
                    ApplicationLogStream.Unknown,
                    message),
                occurredAt,
                row.ReceivedAt,
                structured?.Level ?? ApplicationLogLevel.Unknown,
                // Declared by the application, so never derived. A line without one stays unknown.
                false,
                structured?.Category,
                message.Length == 0 ? "(empty log line)" : message,
                structured?.Exception is { Length: > 0 } exception
                    ? Truncate(exception, MaximumStackTraceLength)
                    : null,
                // This table does not separate standard output from standard error, so claiming either would be
                // inventing a fact. The container app reader can tell them apart; this one cannot.
                ApplicationLogStream.Unknown,
                null,
                NullIfWhiteSpace(row.Host)));
        }

        return entries;
    }

    /// <summary>
    /// Reads the application's own record out of the line, or <c>null</c> when the line is not one.
    /// </summary>
    /// <remarks>
    /// A line is only treated as structured when it parses <em>and</em> declares a <c>LogLevel</c>. JSON that
    /// happens to appear in plain output - a serialized payload written to the console - is not a log record, and
    /// promoting it to one would attach a severity nobody stated.
    /// </remarks>
    private static StructuredLine? ReadStructuredLine(string text)
    {
        if (text.Length == 0 || text[0] != '{')
        {
            return null;
        }

        try
        {
            using JsonDocument document = JsonDocument.Parse(text);
            JsonElement root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object
                || !root.TryGetProperty("LogLevel", out JsonElement level)
                || level.ValueKind != JsonValueKind.String)
            {
                return null;
            }

            return new StructuredLine(
                MapLevel(level.GetString()),
                ReadString(root, "Category"),
                ReadString(root, "Message") ?? string.Empty,
                ReadString(root, "Exception"));
        }
        catch (JsonException)
        {
            // A line that starts like JSON and is not: kept as text rather than discarded, because it is still
            // something the application printed.
            return null;
        }
    }

    private static string? ReadString(JsonElement root, string name) =>
        root.TryGetProperty(name, out JsonElement value) && value.ValueKind == JsonValueKind.String
            ? NullIfWhiteSpace(value.GetString())
            : null;

    /// <summary>
    /// Maps the .NET log level names. An unrecognized name is unknown rather than guessed at.
    /// </summary>
    private static ApplicationLogLevel MapLevel(string? value) => value switch
    {
        "Trace" => ApplicationLogLevel.Trace,
        "Debug" => ApplicationLogLevel.Debug,
        "Information" => ApplicationLogLevel.Information,
        "Warning" => ApplicationLogLevel.Warning,
        "Error" => ApplicationLogLevel.Error,
        "Critical" => ApplicationLogLevel.Critical,
        _ => ApplicationLogLevel.Unknown
    };

    private static string Truncate(string value, int maximum) =>
        value.Length <= maximum ? value : value[..maximum];

    private static string? NullIfWhiteSpace(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value;

    private sealed record StructuredLine(
        ApplicationLogLevel Level,
        string? Category,
        string Message,
        string? Exception);
}
