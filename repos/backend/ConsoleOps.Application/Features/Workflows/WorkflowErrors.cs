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

    internal static readonly Error WorkflowPathRequired = new(
        "Workflows.WorkflowPathRequired",
        "The workflow's definition path is required to read whether it can be run manually.",
        ErrorType.Validation);

    internal static readonly Error UnknownRiskLevel = new(
        "Workflows.UnknownRiskLevel",
        "A workflow's risk must be unclassified, normal, or destructive.",
        ErrorType.Validation);

    internal static readonly Error ReferenceRequired = new(
        "Workflows.ReferenceRequired",
        "A branch or tag is required. Console Ops does not choose one, because a run against an unexpected ref "
        + "is the kind of surprise this refuses to cause.",
        ErrorType.Validation);

    internal static readonly Error RiskNotMarked = new(
        "Workflows.RiskNotMarked",
        "This workflow has no risk level. Console Ops will not run a workflow whose risk nobody has stated.",
        ErrorType.Validation);

    internal static readonly Error ConfirmationRequired = new(
        "Workflows.ConfirmationRequired",
        "This workflow is marked destructive. Type its name to confirm the run.",
        ErrorType.Validation);

    internal static readonly Error ManualRunUnavailable = new(
        "Workflows.ManualRunUnavailable",
        "This workflow does not declare a manual dispatch trigger, or its definition could not be read. Console "
        + "Ops does not start a workflow on a guess.",
        ErrorType.Validation);

    internal static readonly Error WorkflowDisabled = new(
        "Workflows.WorkflowDisabled",
        "The provider reports this workflow as disabled, so it cannot be started.",
        ErrorType.Validation);

    internal static readonly Error DispatchUnauthorized = new(
        "Workflows.Unauthorized",
        "GitHub refused the run. The configured token needs write access to this repository's actions; a "
        + "read-only token can list workflows but not start them.",
        // Forbidden rather than a failure: nothing broke, and calling a missing token scope a server fault sends
        // an operator looking for an outage.
        ErrorType.Forbidden);

    internal static readonly Error DispatchRejected = new(
        "Workflows.DispatchRejected",
        "GitHub rejected the request. The ref may not exist on the repository, or an input may not be one the "
        + "workflow accepts.",
        ErrorType.Validation);

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
            ErrorType.Forbidden),
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
