namespace ConsoleOps.Application.Integrations.AzureMonitor;

/// <summary>
/// Which log lines are framework chatter rather than something the application did.
/// <para>
/// Two rules keep this from hiding anything that matters: only information and below can be noise, so a
/// warning or error from the same category always survives; and a line whose category could not be parsed is
/// never noise, because Console Ops does not know what it is. The measured case that justified it, and the
/// requirement to always report the count, are in <c>docs/Console_Ops_Logs_Plan.md</c>.
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
        "Microsoft.EntityFrameworkCore.",
        "System.Net.Http.HttpClient.",
        "Microsoft.Extensions.Http.",
        "Microsoft.AspNetCore.Routing.",
        "Microsoft.AspNetCore.Mvc.Infrastructure.",
        "Microsoft.AspNetCore.StaticFiles."
    ];

    public static bool IsNoise(ApplicationLogEntry entry)
    {
        ArgumentNullException.ThrowIfNull(entry);

        if (entry.Category is not { Length: > 0 } category)
        {
            return false;
        }

        return IsQuietEnoughToHide(entry.Level)
            && Prefixes.Any(prefix => category.StartsWith(prefix, StringComparison.Ordinal));
    }

    /// <summary>A warning or worse is never hidden, whatever emitted it.</summary>
    private static bool IsQuietEnoughToHide(ApplicationLogLevel level) => level switch
    {
        ApplicationLogLevel.Trace => true,
        ApplicationLogLevel.Debug => true,
        ApplicationLogLevel.Information => true,
        _ => false
    };
}
