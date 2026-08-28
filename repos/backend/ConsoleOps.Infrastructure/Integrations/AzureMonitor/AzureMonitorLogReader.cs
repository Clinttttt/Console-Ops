using Azure;
using Azure.Core;
using Azure.Identity;
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
    /// <summary>
    /// How many raw rows to scan per wanted entry when framework chatter is being excluded. A busy service
    /// logs several infrastructure lines for every line of its own, and a folded entry spans several rows.
    /// </summary>
    private const int NoiseScanFactor = 5;

    /// <summary>
    /// How many hidden categories to name. Enough to explain a quiet window, not a second report.
    /// </summary>
    private const int MaximumNoiseCategories = 4;
    public async Task<ApplicationLogReadResult> ReadAsync(
        ApplicationLogQuery query,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        if (query.WorkspaceId == Guid.Empty
            || !AzureLogSource.IsValidResourceName(query.ContainerAppName, query.Platform))
        {
            // A source that cannot be a real resource for its platform is never sent to the provider.
            return ApplicationLogReadResult.Failed(
                ApplicationLogReadFailure.NotFound,
                timeProvider.GetUtcNow());
        }

        int wanted = options.ClampRows(query.Limit);
        // Framework chatter can be almost all of a window, so filtering has to scan further back or the page
        // arrives empty. The scan is still bounded by the configured row cap, which is what protects the
        // provider and the bill.
        int scan = query.ExcludeNoise ? options.ClampRows(wanted * NoiseScanFactor) : wanted;
        QueryTimeRange range = BuildRange(query);
        string kql = query.Platform switch
        {
            AzureLogPlatform.AppService =>
                AzureAppServiceLogQuery.Build(query.ContainerAppName, scan, query.Search),
            _ => AzureConsoleLogQuery.Build(query.ContainerAppName, scan, query.Search)
        };

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

            AzureConsoleLogRow[] rows = [];
            int rowCount;
            IReadOnlyList<ApplicationLogEntry> normalized;

            if (query.Platform == AzureLogPlatform.AppService)
            {
                AzureAppServiceLogRow[] siteRows = table.Rows.Select(ReadAppServiceRow).ToArray();
                rowCount = siteRows.Length;
                // One record per row: an application logging structured lines has nothing to fold, so there is no
                // boundary fragment to drop either.
                normalized = AzureAppServiceLogNormalizer.Normalize(siteRows);
            }
            else
            {
                rows = table.Rows.Select(ReadRow).ToArray();
                rowCount = rows.Length;
                // Normalized first: continuation lines are folded into the entry that owns them, so filtering
                // afterwards can never orphan a stack trace or attach it to an unrelated line.
                normalized = AzureConsoleLogNormalizer.Normalize(rows);
            }

            bool scanTruncated = rowCount >= scan;
            if (query.Platform != AzureLogPlatform.AppService)
            {
                normalized = DropBoundaryFragment(normalized, scanTruncated);
            }

            if (!query.ExcludeNoise)
            {
                return ApplicationLogReadResult.Success(
                    normalized,
                    scanTruncated,
                    timeProvider.GetUtcNow());
            }

            ApplicationLogEntry[] kept = normalized.Where(entry => !ApplicationLogNoise.IsNoise(entry)).ToArray();
            ApplicationLogEntry[] page = kept.Length > wanted ? [.. kept.Take(wanted)] : kept;
            ApplicationLogNoiseCount[] byCategory = normalized
                .Where(ApplicationLogNoise.IsNoise)
                .GroupBy(entry => entry.Category!, StringComparer.Ordinal)
                .Select(group => new ApplicationLogNoiseCount(group.Key, group.Count()))
                .OrderByDescending(count => count.Count)
                .ThenBy(count => count.Category, StringComparer.Ordinal)
                .Take(MaximumNoiseCategories)
                .ToArray();

            return ApplicationLogReadResult.Success(
                page,
                scanTruncated || kept.Length > wanted,
                timeProvider.GetUtcNow(),
                normalized.Count - kept.Length,
                byCategory);
        }
        catch (RequestFailedException failure)
        {
            return ApplicationLogReadResult.Failed(MapFailure(failure), timeProvider.GetUtcNow());
        }
        catch (AuthenticationFailedException)
        {
            // The identity could not be established at all: no token was ever sent, so this is not the
            // provider refusing the query but Console Ops being unable to ask. It is reported as unauthorized
            // rather than escaping as a fault, because a screen must never render "could not authenticate" as
            // an empty window.
            return ApplicationLogReadResult.Failed(
                ApplicationLogReadFailure.Unauthorized,
                timeProvider.GetUtcNow());
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

    /// <summary>
    /// Removes the oldest entry when the row cap cut the scan mid-entry.
    /// <para>
    /// The cap slices the window at a row, not at an entry, so the oldest lines read can be the tail of a
    /// multi-line entry whose first line was never read. Folding has nothing to attach them to, and the
    /// result is an event with no severity, no category, and a message like <c>LIMIT @p</c> - which is a
    /// fragment presented as a record. The complete entry appears in the next window back, so dropping the
    /// fragment loses nothing.
    /// </para>
    /// <para>
    /// Only when the scan was truncated, and only for the oldest entry. An unprefixed line anywhere else is
    /// a real line of output that simply carried no convention, and it is kept as <c>unknown</c>.
    /// </para>
    /// </summary>
    private static IReadOnlyList<ApplicationLogEntry> DropBoundaryFragment(
        IReadOnlyList<ApplicationLogEntry> entries,
        bool scanTruncated)
    {
        if (!scanTruncated || entries.Count == 0)
        {
            return entries;
        }

        // Entries are newest first, so the cut is at the end.
        ApplicationLogEntry oldest = entries[^1];
        bool carriedNoPrefix = oldest is
        {
            Category: null,
            Level: ApplicationLogLevel.Unknown,
            LevelIsDerived: false
        };

        return carriedNoPrefix ? [.. entries.Take(entries.Count - 1)] : entries;
    }

    private static AzureConsoleLogRow ReadRow(LogsTableRow row) => new(
        ReadOptionalDateTimeOffset(row, "EmittedAt"),
        ReadOptionalDateTimeOffset(row, "ReceivedAt"),
        ReadOptionalString(row, "Message"),
        ReadOptionalString(row, "StreamName"),
        ReadOptionalString(row, "Revision"),
        ReadOptionalString(row, "Replica"));

    private static AzureAppServiceLogRow ReadAppServiceRow(LogsTableRow row) => new(
        ReadOptionalDateTimeOffset(row, "EmittedAt"),
        ReadOptionalDateTimeOffset(row, "ReceivedAt"),
        ReadOptionalString(row, "Message"),
        ReadOptionalString(row, "Replica"));

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
