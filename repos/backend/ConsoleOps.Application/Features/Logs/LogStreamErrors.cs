using ConsoleOps.Application.Abstractions.Messaging;
using ConsoleOps.Application.Integrations.AzureMonitor;

namespace ConsoleOps.Application.Features.Logs;

/// <summary>
/// Stable errors for the log stream.
/// </summary>
/// <remarks>
/// Each one distinguishes a fact the screen must not blur: nothing configured to read, a scope that cannot
/// be read, and a provider that could not be asked. None of them is an empty stream, because an empty
/// stream means the window genuinely held no events.
/// </remarks>
internal static class LogStreamErrors
{
    public static readonly Error NoLogSourceConfigured = new(
        "Logs.NoLogSourceConfigured",
        "No environment has a log source configured, so there is nowhere to read logs from. Add a log "
        + "source to a project environment.",
        ErrorType.NotFound);

    public static readonly Error ScopeNotFound = new(
        "Logs.ScopeNotFound",
        "That project and environment has no log source Console Ops can read.",
        ErrorType.NotFound);

    public static Error From(ApplicationLogReadFailure failure) => failure switch
    {
        ApplicationLogReadFailure.NotFound => new Error(
            "Logs.WorkspaceNotFound",
            "The configured Log Analytics workspace was not found, or the configured identity cannot see "
            + "it.",
            ErrorType.NotFound),
        ApplicationLogReadFailure.Unauthorized => new Error(
            "Logs.Unauthorized",
            "Azure rejected the configured identity. Console Ops needs read access to the workspace.",
            ErrorType.Failure),
        ApplicationLogReadFailure.RateLimited => new Error(
            "Logs.RateLimited",
            "Azure rate limited the log query. Try again shortly.",
            ErrorType.Failure),
        ApplicationLogReadFailure.InvalidResponse => new Error(
            "Logs.InvalidResponse",
            "Azure returned a response Console Ops could not read.",
            ErrorType.Failure),
        _ => new Error(
            "Logs.Unavailable",
            "Azure could not be reached, so the log window could not be read.",
            ErrorType.Failure)
    };
}
