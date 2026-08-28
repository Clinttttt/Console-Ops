using ConsoleOps.Domain.Projects;

namespace ConsoleOps.Application.Integrations.AzureMonitor;

/// <summary>
/// Reads an environment's application logs from its provider.
/// <para>
/// Console Ops pulls logs as it pulls everything else. Nothing pushes into Console Ops, so this port is
/// read-only and the adapter behind it owns every provider detail: the query language, the SDK, and the
/// normalization of provider text into the models below.
/// </para>
/// </summary>
public interface IApplicationLogReader
{
    Task<ApplicationLogReadResult> ReadAsync(
        ApplicationLogQuery query,
        CancellationToken cancellationToken);
}

/// <summary>
/// What to ask the provider for. Every bound is explicit, because a log query is the one read in Console
/// Ops that can cost money and return an unbounded amount of text.
/// </summary>
/// <param name="WorkspaceId">Log Analytics workspace holding the resource's console output.</param>
/// <param name="ContainerAppName">The Azure resource whose logs belong to the environment being read.</param>
/// <param name="Platform">
/// Which Azure service hosts it. The adapter reads a different table per platform, so this is not decoration:
/// asking the wrong table returns an empty stream, which is indistinguishable from a quiet service.
/// </param>
/// <param name="FromUtc">Start of the window, inclusive.</param>
/// <param name="ToUtc">End of the window, exclusive. Paging moves this back through the window.</param>
/// <param name="Limit">Maximum entries to return. The adapter clamps it.</param>
/// <param name="Search">
/// Free text to match within the log line, or <c>null</c>. Operator-supplied, so the adapter is
/// responsible for making it safe to send.
/// </param>
/// <param name="ExcludeNoise">
/// When <c>true</c> the adapter leaves out framework chatter and scans further back to fill the page, so a
/// readable stream does not cost the caller extra round trips. The port's default is to return everything:
/// filtering is a decision the screen makes, not one the provider layer assumes.
/// </param>
public sealed record ApplicationLogQuery(
    Guid WorkspaceId,
    string ContainerAppName,
    DateTimeOffset FromUtc,
    DateTimeOffset ToUtc,
    int Limit,
    string? Search = null,
    bool ExcludeNoise = false,
    AzureLogPlatform Platform = AzureLogPlatform.ContainerApp);

/// <summary>
/// Entries newest first, or the failure that prevented reading them.
/// </summary>
/// <param name="Truncated">
/// <c>true</c> when the limit cut the result, so the screen can say the window holds more.
/// </param>
/// <param name="NoiseHidden">
/// How many entries were left out as framework chatter. Reported so the screen can say why a window looks
/// quiet instead of appearing to have found nothing.
/// </param>
/// <param name="NoiseByCategory">
/// The categories behind that count, largest first. An operator whose window held nothing but chatter still
/// learns what the service was doing, which is the difference between an idle service and a broken read.
/// </param>
public sealed record ApplicationLogReadResult(
    IReadOnlyList<ApplicationLogEntry> Entries,
    bool Truncated,
    ApplicationLogReadFailure? Failure,
    DateTimeOffset ObservedAtUtc,
    int NoiseHidden = 0,
    IReadOnlyList<ApplicationLogNoiseCount>? NoiseByCategory = null)
{
    public bool IsSuccess => Failure is null;

    public static ApplicationLogReadResult Success(
        IReadOnlyList<ApplicationLogEntry> entries,
        bool truncated,
        DateTimeOffset observedAtUtc,
        int noiseHidden = 0,
        IReadOnlyList<ApplicationLogNoiseCount>? noiseByCategory = null) =>
        new(entries, truncated, null, observedAtUtc, noiseHidden, noiseByCategory);

    public static ApplicationLogReadResult Failed(
        ApplicationLogReadFailure failure,
        DateTimeOffset observedAtUtc) =>
        new([], false, failure, observedAtUtc);
}

/// <summary>
/// How many lines one category contributed to what was left out.
/// </summary>
public sealed record ApplicationLogNoiseCount(string Category, int Count);

/// <summary>
/// Why a read did not produce entries. Mirrors the GitHub reader's failure vocabulary so the API can
/// distinguish "nothing to show" from "could not ask", which are different facts.
/// </summary>
public enum ApplicationLogReadFailure
{
    Unauthorized,
    NotFound,
    RateLimited,
    Unavailable,
    InvalidResponse
}

/// <summary>
/// One log line, normalized.
/// <para>
/// Console logs are text. Severity and category are parsed from the line when the emitter followed a
/// recognizable convention, and <see cref="LevelIsDerived"/> records that they were inferred rather than
/// declared. Structure that a console line cannot carry - trace ids, properties, exception objects - is
/// absent here and stays absent until a richer source exists.
/// </para>
/// </summary>
/// <param name="Id">
/// Deterministic identity synthesized by the adapter. The provider exposes no stable row id, and the UI
/// needs one to keep a selection across pages.
/// </param>
/// <param name="Level">Severity as parsed, or <see cref="ApplicationLogLevel.Unknown"/>.</param>
/// <param name="Category">Emitter category such as <c>Spinner.Payments</c>, or <c>null</c>.</param>
/// <param name="StackTrace">Continuation lines that belonged to this entry, or <c>null</c>.</param>
/// <param name="ReceivedAtUtc">
/// When the provider ingested the line, or <c>null</c> when it did not say. Kept beside
/// <paramref name="OccurredAtUtc"/> so clock skew and ingestion delay stay visible instead of being
/// smoothed away.
/// </param>
public sealed record ApplicationLogEntry(
    string Id,
    DateTimeOffset OccurredAtUtc,
    DateTimeOffset? ReceivedAtUtc,
    ApplicationLogLevel Level,
    bool LevelIsDerived,
    string? Category,
    string Message,
    string? StackTrace,
    ApplicationLogStream Stream,
    string? Revision,
    string? Replica);

public enum ApplicationLogLevel
{
    Trace,
    Debug,
    Information,
    Warning,
    Error,
    Critical,
    Unknown
}

public enum ApplicationLogStream
{
    Stdout,
    Stderr,
    Unknown
}
