using Azure;
using Azure.Core;
using Azure.Monitor.Query;
using Azure.Monitor.Query.Models;
using ConsoleOps.Application.Integrations.AzureMonitor;
using ConsoleOps.Domain.Projects;

namespace ConsoleOps.Infrastructure.Integrations.AzureMonitor;

/// <summary>
/// Reads container console logs from Azure Monitor Log Analytics.
/// <para>
/// Provider types stop here: the SDK, the query text, and the row shape stay inside this adapter, and the
/// port's models are what leaves it. Console Ops asks with a read-only credential and never writes.
/// </para>
/// <para>
/// Bounds are the point. A log query is the one read that can cost money and return unbounded text, so the
/// window, the row count, and the wait are all limited here rather than trusted from a caller.
/// </para>
/// </summary>
internal sealed class AzureMonitorLogReader(
    LogsQueryClient client,
    TimeProvider timeProvider,
    AzureMonitorOptions options) : IApplicationLogReader
{
    public async Task<ApplicationLogReadResult> ReadAsync(
        ApplicationLogQuery query,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        if (query.WorkspaceId == Guid.Empty
            || !AzureLogSource.IsValidContainerAppName(query.ContainerAppName))
        {
            // A source that cannot be a real app is never sent to the provider.
            return ApplicationLogReadResult.Failed(
                ApplicationLogReadFailure.NotFound,
                timeProvider.GetUtcNow());
        }

        int limit = options.ClampRows(query.Limit);
        QueryTimeRange range = BuildRange(query);
        string kql = AzureConsoleLogQuery.Build(query.ContainerAppName, limit, query.Search);

        try
        {
            using CancellationTokenSource timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeout.CancelAfter(options.QueryTimeout);

            Response<LogsQueryResult> response = await client.QueryWorkspaceAsync(
                query.WorkspaceId.ToString(),
                kql,
                range,
                cancellationToken: timeout.Token);
            LogsTable? table = response.Value.Table;
            if (table is null)
            {
                return ApplicationLogReadResult.Failed(
                    ApplicationLogReadFailure.InvalidResponse,
                    timeProvider.GetUtcNow());
            }

            AzureConsoleLogRow[] rows = table.Rows.Select(ReadRow).ToArray();
            return ApplicationLogReadResult.Success(
                AzureConsoleLogNormalizer.Normalize(rows),
                rows.Length >= limit,
                timeProvider.GetUtcNow());
        }
        catch (RequestFailedException failure)
        {
            return ApplicationLogReadResult.Failed(MapFailure(failure), timeProvider.GetUtcNow());
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return ApplicationLogReadResult.Failed(
                ApplicationLogReadFailure.Unavailable,
                timeProvider.GetUtcNow());
        }
    }

    /// <summary>
    /// Clamps the requested window so paging cannot ask Azure to scan more than the configured span.
    /// </summary>
    private QueryTimeRange BuildRange(ApplicationLogQuery query)
    {
        DateTimeOffset end = query.ToUtc;
        DateTimeOffset earliestAllowed = end - options.MaximumWindow;
        DateTimeOffset start = query.FromUtc < earliestAllowed ? earliestAllowed : query.FromUtc;

        return start >= end
            ? new QueryTimeRange(end - options.MaximumWindow, end)
            : new QueryTimeRange(start, end);
    }

    private static AzureConsoleLogRow ReadRow(LogsTableRow row) => new(
        ReadOptionalDateTimeOffset(row, "TimeGenerated"),
        ReadOptionalString(row, "Log"),
        ReadOptionalString(row, "Stream"),
        ReadOptionalString(row, "RevisionName"),
        ReadOptionalString(row, "ContainerGroupName"));

    private static string? ReadOptionalString(LogsTableRow row, string column)
    {
        try
        {
            return row.GetString(column);
        }
        catch (ArgumentOutOfRangeException)
        {
            // A workspace that does not expose the column is missing a fact, not broken.
            return null;
        }
    }

    private static DateTimeOffset? ReadOptionalDateTimeOffset(LogsTableRow row, string column)
    {
        try
        {
            return row.GetDateTimeOffset(column);
        }
        catch (ArgumentOutOfRangeException)
        {
            return null;
        }
        catch (InvalidCastException)
        {
            return null;
        }
    }

    private static ApplicationLogReadFailure MapFailure(RequestFailedException failure) => failure.Status switch
    {
        401 or 403 => ApplicationLogReadFailure.Unauthorized,
        404 => ApplicationLogReadFailure.NotFound,
        429 => ApplicationLogReadFailure.RateLimited,
        >= 500 => ApplicationLogReadFailure.Unavailable,
        400 => ApplicationLogReadFailure.InvalidResponse,
        _ => ApplicationLogReadFailure.Unavailable
    };
}

/// <summary>
/// Limits and credentials for reading Azure logs. Credentials are never per project: one Console Ops
/// identity with <c>Log Analytics Reader</c> on the workspace is all this needs.
/// </summary>
public sealed class AzureMonitorOptions
{
    public const string SectionName = "Azure:Monitor";

    internal const int DefaultMaximumRows = 1_000;
    internal const int DefaultMaximumWindowHours = 24;
    internal const int DefaultQueryTimeoutSeconds = 20;

    /// <summary>Upper bound on rows returned by one query.</summary>
    public int MaximumRows { get; set; } = DefaultMaximumRows;

    /// <summary>Longest window a single query may scan.</summary>
    public int MaximumWindowHours { get; set; } = DefaultMaximumWindowHours;

    public int QueryTimeoutSeconds { get; set; } = DefaultQueryTimeoutSeconds;

    internal TimeSpan MaximumWindow =>
        TimeSpan.FromHours(Math.Clamp(MaximumWindowHours, 1, 168));

    internal TimeSpan QueryTimeout =>
        TimeSpan.FromSeconds(Math.Clamp(QueryTimeoutSeconds, 1, 120));

    internal int ClampRows(int requested) => Math.Clamp(requested, 1, Math.Clamp(MaximumRows, 1, 5_000));
}
