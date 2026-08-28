namespace ConsoleOps.Infrastructure.Integrations.AzureMonitor;

/// <summary>
/// Builds the KQL that reads an App Service site's console output.
/// <para>
/// Verified against rows from a live site rather than against documentation, because the obvious reading of
/// this table is wrong in a way that would not have shown up as an error. Two things matter:
/// </para>
/// <para>
/// <c>Level</c> is not the application's severity. Every row carries <c>Informational</c>, because it describes
/// App Service's console stream and not the line inside it. A reader that projected <c>Level</c> would report
/// every warning as information and look entirely correct while doing it.
/// </para>
/// <para>
/// The line itself is <c>ResultDescription</c>. Where the application logs structured JSON that carries its own
/// <c>LogLevel</c> and <c>Category</c>; where it does not, the text is all there is. Both are passed through
/// unparsed and sorted out by the normalizer, so this query stays a projection and nothing here decides what a
/// line means.
/// </para>
/// <para>
/// The site is matched on <c>_ResourceId</c>, which is the resource path Azure stamps on every row, so a
/// workspace shared by several sites returns only the one asked for. The shape is fixed and the values are the
/// only variable part: the site name comes from validated configuration, and free text is emitted as an escaped
/// KQL string literal. Nothing is concatenated in raw.
/// </para>
/// </summary>
internal static class AzureAppServiceLogQuery
{
    internal const string TableName = "AppServiceConsoleLogs";

    public static string Build(string siteName, int limit, string? search)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(siteName);
        ArgumentOutOfRangeException.ThrowIfLessThan(limit, 1);

        // Matched on the resource path's tail rather than on a name column, because this table has none. The
        // leading separator keeps a site called "api" from matching one called "public-api".
        string suffix = AzureConsoleLogQuery.Literal($"/sites/{siteName.ToLowerInvariant()}");
        string filter = string.IsNullOrWhiteSpace(search)
            ? string.Empty
            : $" | where Message contains {AzureConsoleLogQuery.Literal(search.Trim())}";

        // The time window is applied by the query API's timespan rather than in text, so the window and the
        // billed scan cannot disagree.
        return $"""
            {TableName}
                | where tolower(tostring(_ResourceId)) endswith {suffix}
                | project EmittedAt = TimeGenerated,
                          ReceivedAt = TimeGenerated,
                          Message = ResultDescription,
                          Replica = Host
                | where isnotempty(Message){filter}
                | order by EmittedAt desc
                | take {limit}
            """;
    }
}
