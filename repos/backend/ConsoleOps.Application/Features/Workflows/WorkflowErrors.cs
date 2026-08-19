using ConsoleOps.Application.Abstractions.Messaging;
using ConsoleOps.Application.Integrations.GitHub;

namespace ConsoleOps.Application.Features.Workflows;

/// <summary>
/// Maps a provider read failure to a stable application error.
/// </summary>
/// <remarks>
/// Each one names what went wrong and what to check, without leaking the token, the repository URL, or a raw
/// provider payload. An exhausted rate limit is reported as itself rather than as a rejected credential, because
/// those send an operator to fix entirely different things.
/// </remarks>
internal static class WorkflowErrors
{
    internal static readonly Error ProjectNotFound = new(
        "Workflows.ProjectNotFound",
        "No registered project owns that run.",
        ErrorType.NotFound);

    internal static Error From(GitHubReadFailure failure) => failure switch
    {
        GitHubReadFailure.NotFound => new Error(
            "Workflows.NotFound",
            "GitHub does not expose that repository or run to the configured token.",
            ErrorType.NotFound),
        GitHubReadFailure.Unauthorized => new Error(
            "Workflows.Unauthorized",
            "GitHub rejected the configured token. Console Ops needs a token with read access to the "
            + "repository's actions.",
            ErrorType.Failure),
        GitHubReadFailure.RateLimited => new Error(
            "Workflows.RateLimited",
            "GitHub rate limited the request. Try again shortly.",
            ErrorType.Failure),
        GitHubReadFailure.InvalidResponse => new Error(
            "Workflows.InvalidResponse",
            "GitHub returned a response Console Ops could not read.",
            ErrorType.Failure),
        _ => new Error(
            "Workflows.Unavailable",
            "GitHub could not be reached.",
            ErrorType.Failure)
    };
}
