namespace ConsoleOps.Infrastructure.Integrations.AzureMonitor;

/// <summary>
/// Builds the KQL that reads a container app's console logs.
/// <para>
/// The shape is fixed and the values are the only variable part, because this query carries
/// operator-supplied text. Identifiers come from validated configuration; free text is emitted as an
/// escaped KQL string literal. Nothing is concatenated in raw.
/// </para>
/// <para>
/// Only the documented columns of <c>ContainerAppConsoleLogs</c> are projected, and the query stays
/// within the operator set that Basic-tier tables allow: <c>where</c>, <c>project</c>, <c>order</c>,
/// <c>take</c>. No joins, no aggregation.
/// </para>
/// </summary>
internal static class AzureConsoleLogQuery
{
    internal const string TableName = "ContainerAppConsoleLogs";

    public static string Build(string containerAppName, int limit, string? search)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(containerAppName);
        ArgumentOutOfRangeException.ThrowIfLessThan(limit, 1);

        // The time window is applied by the query API's timespan rather than in text, so the window and
        // the billed scan cannot disagree.
        string query = $"""
            {TableName}
            | where ContainerAppName == {Literal(containerAppName)}
            """;

        if (!string.IsNullOrWhiteSpace(search))
        {
            query += $"""

                | where Log contains {Literal(search.Trim())}
                """;
        }

        query += $"""

            | project TimeGenerated, Log, Stream, RevisionName, ContainerGroupName
            | order by TimeGenerated desc
            | take {limit}
            """;

        return query;
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
