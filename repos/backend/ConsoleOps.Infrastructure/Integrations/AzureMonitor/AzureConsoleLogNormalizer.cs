using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using ConsoleOps.Application.Integrations.AzureMonitor;

namespace ConsoleOps.Infrastructure.Integrations.AzureMonitor;

/// <summary>
/// Turns container console output into log entries.
/// <para>
/// A console row is a line of text plus runtime identity: the provider records no severity, category,
/// trace id, or exception object. What can be recovered is recovered from the convention the .NET console
/// logger writes - <c>warn: Spinner.Payments[0]</c> followed by indented message lines - and everything
/// recovered that way is marked as derived. When the convention is absent the level is
/// <see cref="ApplicationLogLevel.Unknown"/>: a line of plain output is not evidence of information.
/// </para>
/// <para>
/// Rows arrive newest first and one per line, so an exception's stack trace spans several rows. Folding
/// them is a rule rather than a guess: a row that does not open a new prefixed entry belongs to the entry
/// above it.
/// </para>
/// </summary>
internal static partial class AzureConsoleLogNormalizer
{
    private const int MaximumMessageLength = 4_000;
    private const int MaximumStackTraceLength = 8_000;

    /// <param name="rows">Provider rows, newest first, as the query returns them.</param>
    public static IReadOnlyList<ApplicationLogEntry> Normalize(IReadOnlyList<AzureConsoleLogRow> rows)
    {
        ArgumentNullException.ThrowIfNull(rows);

        // Fold in chronological order so a continuation line can attach to the entry it followed, then
        // hand back newest first, which is how the stream is read.
        List<ApplicationLogEntry> entries = [];
        List<string> pendingContinuation = [];
        AzureConsoleLogRow? openRow = null;
        ParsedPrefix? openPrefix = null;
        int ordinalWithinTimestamp = 0;
        DateTimeOffset lastTimestamp = default;

        foreach (AzureConsoleLogRow row in rows.Reverse())
        {
            string line = row.Log ?? string.Empty;
            ParsedPrefix? prefix = ParsePrefix(line);

            if (prefix is null && openRow is not null)
            {
                pendingContinuation.Add(line.TrimEnd());
                continue;
            }

            Flush(entries, openRow, openPrefix, pendingContinuation, ref ordinalWithinTimestamp, ref lastTimestamp);
            openRow = row;
            openPrefix = prefix;
            pendingContinuation.Clear();
        }

        Flush(entries, openRow, openPrefix, pendingContinuation, ref ordinalWithinTimestamp, ref lastTimestamp);
        entries.Reverse();
        return entries;
    }

    private static void Flush(
        List<ApplicationLogEntry> entries,
        AzureConsoleLogRow? row,
        ParsedPrefix? prefix,
        List<string> continuation,
        ref int ordinalWithinTimestamp,
        ref DateTimeOffset lastTimestamp)
    {
        if (row is null)
        {
            return;
        }

        DateTimeOffset occurredAt = row.EmittedAt ?? row.ReceivedAt ?? lastTimestamp;
        ordinalWithinTimestamp = occurredAt == lastTimestamp ? ordinalWithinTimestamp + 1 : 0;
        lastTimestamp = occurredAt;

        ApplicationLogStream stream = ParseStream(row.Stream);
        string[] continuationLines = continuation
            .Where(line => line.Length > 0)
            .ToArray();
        string message = prefix is null
            ? Truncate((row.Log ?? string.Empty).Trim(), MaximumMessageLength)
            : ComposeMessage(prefix, continuationLines);
        string? stackTrace = prefix is null
            ? ComposeStackTrace(continuationLines)
            : ComposeStackTrace(continuationLines.Where(IsStackFrame).ToArray());

        (ApplicationLogLevel level, bool derived) = ResolveLevel(prefix, stream);

        entries.Add(new ApplicationLogEntry(
            ComposeId(occurredAt, ordinalWithinTimestamp, row.ContainerGroupName, stream, message),
            occurredAt,
            row.ReceivedAt,
            level,
            derived,
            prefix?.Category,
            message.Length == 0 ? "(empty log line)" : message,
            stackTrace,
            stream,
            NullIfWhiteSpace(row.RevisionName),
            NullIfWhiteSpace(row.ContainerGroupName)));
    }

    /// <summary>
    /// The message is the prefixed line's own text when it carried any, otherwise the first continuation
    /// line, which is where the .NET console logger puts it.
    /// </summary>
    private static string ComposeMessage(ParsedPrefix prefix, IReadOnlyList<string> continuation)
    {
        if (prefix.Message.Length > 0)
        {
            return Truncate(prefix.Message, MaximumMessageLength);
        }

        string? firstMessageLine = continuation.FirstOrDefault(line => !IsStackFrame(line));
        return Truncate((firstMessageLine ?? string.Empty).Trim(), MaximumMessageLength);
    }

    private static string? ComposeStackTrace(IReadOnlyList<string> lines) =>
        lines.Count == 0 ? null : Truncate(string.Join('\n', lines), MaximumStackTraceLength);

    /// <summary>
    /// Severity from the console prefix. Without one, <c>stderr</c> is the only evidence available and is
    /// reported as a derived error; plain <c>stdout</c> stays unknown rather than being called information.
    /// </summary>
    private static (ApplicationLogLevel Level, bool Derived) ResolveLevel(
        ParsedPrefix? prefix,
        ApplicationLogStream stream)
    {
        if (prefix is not null)
        {
            return (prefix.Level, true);
        }

        return stream == ApplicationLogStream.Stderr
            ? (ApplicationLogLevel.Error, true)
            : (ApplicationLogLevel.Unknown, false);
    }

    /// <summary>
    /// A stable id for a row the provider does not identify. Deterministic, so the same line keeps the
    /// same id across pages and polls; it is Console Ops' id and never presented as the provider's.
    /// </summary>
    internal static string ComposeId(
        DateTimeOffset occurredAt,
        int ordinal,
        string? replica,
        ApplicationLogStream stream,
        string message)
    {
        string seed = string.Join(
            '\u001f',
            occurredAt.UtcTicks.ToString(CultureInfo.InvariantCulture),
            ordinal.ToString(CultureInfo.InvariantCulture),
            replica ?? string.Empty,
            stream.ToString(),
            message);
        byte[] hash = SHA256.HashData(Encoding.UTF8.GetBytes(seed));
        return Convert.ToHexStringLower(hash.AsSpan(0, 12));
    }

    private static ParsedPrefix? ParsePrefix(string line)
    {
        Match match = ConsolePrefixPattern().Match(line);
        if (!match.Success)
        {
            return null;
        }

        ApplicationLogLevel? level = MapLevel(match.Groups["level"].Value);
        return level is null
            ? null
            : new ParsedPrefix(
                level.Value,
                NullIfWhiteSpace(match.Groups["category"].Value),
                match.Groups["message"].Value.Trim());
    }

    private static ApplicationLogLevel? MapLevel(string token) => token switch
    {
        "trce" or "trace" => ApplicationLogLevel.Trace,
        "dbug" or "debug" => ApplicationLogLevel.Debug,
        "info" or "information" => ApplicationLogLevel.Information,
        "warn" or "warning" => ApplicationLogLevel.Warning,
        "fail" or "error" => ApplicationLogLevel.Error,
        "crit" or "critical" => ApplicationLogLevel.Critical,
        _ => null
    };

    private static ApplicationLogStream ParseStream(string? value) =>
        value?.Trim().ToLowerInvariant() switch
        {
            "stdout" => ApplicationLogStream.Stdout,
            "stderr" => ApplicationLogStream.Stderr,
            _ => ApplicationLogStream.Unknown
        };

    private static bool IsStackFrame(string line)
    {
        string trimmed = line.TrimStart();
        return trimmed.StartsWith("at ", StringComparison.Ordinal)
            || trimmed.StartsWith("--- End of", StringComparison.Ordinal)
            || trimmed.StartsWith("---> ", StringComparison.Ordinal);
    }

    private static string Truncate(string value, int maximumLength) =>
        value.Length <= maximumLength ? value : value[..maximumLength];

    private static string? NullIfWhiteSpace(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    /// <summary>
    /// The .NET console logger's prefix: a level token, then optionally the category with its event id in
    /// brackets, then whatever text remained on that line.
    /// <para>
    /// The category is recognized only when the bracketed event id follows it. Without that anchor, a line
    /// like <c>warn: Provider request required a retry</c> would have its message misread as a category.
    /// </para>
    /// </summary>
    [GeneratedRegex(
        @"^(?<level>trce|dbug|info|warn|fail|crit|trace|debug|information|warning|error|critical)\s*:\s*(?:(?<category>[^\s\[][^\[\]]*)\[\d+\]\s*)?(?<message>.*)$",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex ConsolePrefixPattern();

    private sealed record ParsedPrefix(ApplicationLogLevel Level, string? Category, string Message);
}

/// <summary>One row of container console output, as the adapter reads it.</summary>
/// <param name="EmittedAt">
/// The container's own timestamp. Ordering and display use this, because ingestion time is shared across a
/// whole batch and would scramble the lines within it.
/// </param>
/// <param name="ReceivedAt">When Azure ingested the row, kept so clock skew stays visible.</param>
internal sealed record AzureConsoleLogRow(
    DateTimeOffset? EmittedAt,
    DateTimeOffset? ReceivedAt,
    string? Log,
    string? Stream,
    string? RevisionName,
    string? ContainerGroupName);
