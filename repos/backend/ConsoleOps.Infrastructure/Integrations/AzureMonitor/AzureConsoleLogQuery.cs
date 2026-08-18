namespace ConsoleOps.Infrastructure.Integrations.AzureMonitor;

/// <summary>
/// Builds the KQL that reads a container app's console logs.
/// <para>
/// Two table shapes exist in the wild and both are read. Newer workspaces use the resource-specific
/// <c>ContainerAppConsoleLogs</c> with plain column names; others write to the legacy
/// <c>ContainerAppConsoleLogs_CL</c> with <c>_s</c> suffixes. <c>union isfuzzy=true</c> tolerates whichever
/// is absent, so one query serves both instead of Console Ops guessing and returning an empty stream.
/// </para>
/// <para>
/// Ordering is by the emitter's own timestamp, not by ingestion time. In the legacy table every row of one
/// ingestion batch shares a single <c>TimeGenerated</c>, so ordering by that scrambles the lines within a
/// batch - a message and its own stack trace can arrive interleaved. <c>time_t</c> carries the container's
/// emit time with sub-microsecond precision and restores the true order.
/// </para>
/// <para>
/// The shape is fixed and the values are the only variable part, because this query carries
/// operator-supplied text. The container app name comes from validated configuration; free text is emitted
/// as an escaped KQL string literal. Nothing is concatenated in raw.
/// </para>
/// </summary>
internal static class AzureConsoleLogQuery
{
    internal const string TableName = "ContainerAppConsoleLogs";
    internal const string LegacyTableName = "ContainerAppConsoleLogs_CL";

    public static string Build(string containerAppName, int limit, string? search)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(containerAppName);
        ArgumentOutOfRangeException.ThrowIfLessThan(limit, 1);

        string app = Literal(containerAppName);
        string filter = string.IsNullOrWhiteSpace(search)
            ? string.Empty
            : $" | where Message contains {Literal(search.Trim())}";

        // The time window is applied by the query API's timespan rather than in text, so the window and
        // the billed scan cannot disagree.
        return $"""
            union isfuzzy=true
                ({TableName}
                    | where ContainerAppName =~ {app}
                    | project EmittedAt = TimeGenerated,
                              ReceivedAt = TimeGenerated,
                              Message = Log,
                              StreamName = Stream,
                              Revision = RevisionName,
                              Replica = ContainerGroupName),
                ({LegacyTableName}
                    | where ContainerAppName_s =~ {app}
                    | project EmittedAt = coalesce(time_t, TimeGenerated),
                              ReceivedAt = TimeGenerated,
                              Message = Log_s,
                              StreamName = Stream_s,
                              Revision = RevisionName_s,
                              Replica = ContainerGroupName_s)
            | where isnotempty(Message){filter}
            | order by EmittedAt desc
            | take {limit}
            """;
    }

    /// <summary>
    /// Emits a value as a KQL string literal.
    /// <para>
    /// Backslashes and quotes are escaped, and the control characters that could end a statement or start
    /// a comment are encoded rather than passed through. A log screen accepts free text, so this is the
    /// boundary that keeps that text from becoming query syntax.
    /// </para>
    /// </summary>
    internal static string Literal(string value)
    {
        System.Text.StringBuilder escaped = new(value.Length + 2);
        escaped.Append('"');

        foreach (char character in value)
        {
            switch (character)
            {
                case '\\':
                    escaped.Append("\\\\");
                    break;
                case '"':
                    escaped.Append("\\\"");
                    break;
                case '\r':
                    escaped.Append("\\r");
                    break;
                case '\n':
                    escaped.Append("\\n");
                    break;
                case '\t':
                    escaped.Append("\\t");
                    break;
                default:
                    if (char.IsControl(character))
                    {
                        escaped.Append("\\u").Append(((int)character).ToString("x4"));
                    }
                    else
                    {
                        escaped.Append(character);
                    }

                    break;
            }
        }

        escaped.Append('"');
        return escaped.ToString();
    }
}
