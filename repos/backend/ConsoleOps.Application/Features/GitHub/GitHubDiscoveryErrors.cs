using ConsoleOps.Application.Abstractions.Messaging;
using ConsoleOps.Application.Integrations.GitHub;

namespace ConsoleOps.Application.Features.GitHub;

/// <summary>
/// Maps a provider read failure to a stable application error.
/// </summary>
/// <remarks>
/// The descriptions name what went wrong without leaking the credential, the target URL, or any raw
/// provider payload. A missing or rejected credential is a server configuration fault, not the
/// caller's mistake, so it is reported as a failure rather than as invalid input.
/// </remarks>
internal static class GitHubDiscoveryErrors
{
    public static Error From(GitHubReadFailure failure) => failure switch
    {
        GitHubReadFailure.NotFound => new Error(
            "GitHub.NotFound",
            "GitHub does not expose that repository to the configured credential.",
            ErrorType.NotFound),
        GitHubReadFailure.Unauthorized => new Error(
            "GitHub.Unauthorized",
            "GitHub rejected the configured credential. Check the GitHub token configuration.",
            ErrorType.Failure),
        GitHubReadFailure.RateLimited => new Error(
            "GitHub.RateLimited",
            "GitHub rate limited the request. Try again shortly.",
            ErrorType.Failure),
        GitHubReadFailure.InvalidResponse => new Error(
            "GitHub.InvalidResponse",
            "GitHub returned a response Console Ops could not read.",
            ErrorType.Failure),
        _ => new Error(
            "GitHub.Unavailable",
            "GitHub could not be reached.",
            ErrorType.Failure)
    };
}
