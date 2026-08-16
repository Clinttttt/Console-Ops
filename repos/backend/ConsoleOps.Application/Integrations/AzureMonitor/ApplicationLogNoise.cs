namespace ConsoleOps.Application.Integrations.AzureMonitor;

/// <summary>
/// Which log lines are framework chatter rather than something the application did.
/// <para>
/// A real console stream is dominated by infrastructure logging. An idle ASP.NET Core service with a
/// background poller emits nothing but "Executed DbCommand" every few seconds, which buries the lines an
/// operator opened the screen for. Excluding those by category is what makes the stream readable.
/// </para>
/// <para>
/// Two rules keep this from hiding anything that matters. Only <b>information and below</b> can ever be
/// noise: a warning or an error from the same category is a real event and always survives. And a line whose
/// category could not be parsed is never treated as noise, because Console Ops does not know what it is.
/// </para>
/// <para>
/// The policy is provider-independent - it is about .NET logging conventions, not about Azure - and the
/// screen always reports how many lines it removed, so filtering is visible rather than silent.
/// </para>
/// </summary>
public static class ApplicationLogNoise
{
    /// <summary>
    /// Category prefixes whose informational output describes how the application talked to its
    /// dependencies rather than what it did.
    /// </summary>
    private static readonly string[] Prefixes =
    [
        // Every EF Core category: command text, connections, transactions, query compilation, migrations.
        "Microsoft.EntityFrameworkCore.",
        // The HttpClient factory's request/response pair, logged twice per outbound call.
        "System.Net.Http.HttpClient.",
        "Microsoft.Extensions.Http.",
        // Hosting lifecycle chatter. "Application started" is useful; the middleware bookkeeping is not.
        "Microsoft.AspNetCore.Routing.",
        "Microsoft.AspNetCore.Mvc.Infrastructure.",
        "Microsoft.AspNetCore.StaticFiles."
    ];

    /// <summary>
    /// <c>true</c> when this entry is framework chatter that may be left out of a readable stream.
    /// </summary>
    public static bool IsNoise(ApplicationLogEntry entry)
    {
        ArgumentNullException.ThrowIfNull(entry);

        if (entry.Category is not { Length: > 0 } category)
        {
            // An unparsed line is an unknown, and an unknown is never assumed to be unimportant.
            return false;
        }

        return IsQuietEnoughToHide(entry.Level)
            && Prefixes.Any(prefix => category.StartsWith(prefix, StringComparison.Ordinal));
    }

    /// <summary>
    /// Severities that may be hidden. A warning or worse is never hidden, whatever emitted it: a failed
    /// database command is exactly what an operator is looking for.
    /// </summary>
    private static bool IsQuietEnoughToHide(ApplicationLogLevel level) => level switch
    {
        ApplicationLogLevel.Trace => true,
        ApplicationLogLevel.Debug => true,
        ApplicationLogLevel.Information => true,
        _ => false
    };
}
