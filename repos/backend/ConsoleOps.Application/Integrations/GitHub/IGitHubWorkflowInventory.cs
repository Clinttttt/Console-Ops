namespace ConsoleOps.Application.Integrations.GitHub;

/// <summary>
/// Where a run is in its life, as the provider reports it.
/// </summary>
/// <remarks>
/// Kept separate from the conclusion because they answer different questions, and a provider status Console Ops
/// does not recognise becomes <see cref="Unknown"/> rather than being folded into a state it never reported.
/// </remarks>
public enum GitHubRunStatus
{
    Queued,
    InProgress,
    Waiting,
    Completed,
    Unknown
}

/// <summary>How a completed run ended. Absent while it has not completed.</summary>
public enum GitHubRunConclusion
{
    Passed,
    Failed,
    Cancelled,
    Skipped,
    TimedOut,
    ActionRequired,
    Neutral
}

/// <param name="Number">The provider's run number, which is what an operator refers to elsewhere.</param>
/// <param name="Event">
/// The provider's own trigger event, carried as it was reported. Never read as evidence of a workflow's
/// purpose: a deployment can run on push exactly as a test suite can.
/// </param>
public sealed record GitHubRunSummary(
    long RunId,
    int? Number,
    GitHubRunStatus Status,
    GitHubRunConclusion? Conclusion,
    string Branch,
    string CommitSha,
    string Event,
    string? Actor,
    DateTimeOffset? StartedAtUtc,
    DateTimeOffset? CompletedAtUtc,
    string? RunUrl);

/// <param name="Active">
/// Whether the provider still exposes this workflow. A disabled workflow is reported, never hidden, and is not
/// a failure.
/// </param>
/// <param name="SupportsManualRun">
/// <c>true</c> only where the provider reports a manual dispatch trigger, <c>false</c> where it reports none,
/// and <c>null</c> when the definition could not be read - which is not the same as knowing it cannot be run.
/// </param>
/// <param name="LatestRun">The most recent run, or <c>null</c> when the provider reports none.</param>
public sealed record GitHubWorkflowDefinition(
    long WorkflowId,
    string Name,
    string Path,
    bool Active,
    bool? SupportsManualRun,
    GitHubRunSummary? LatestRun);

public sealed record GitHubWorkflowInventoryPage(IReadOnlyList<GitHubWorkflowDefinition> Workflows);

/// <param name="HasMore">
/// Whether the provider reported runs beyond this page, so a screen can say the list is recent history rather
/// than all of it.
/// </param>
public sealed record GitHubRunPage(IReadOnlyList<GitHubRunSummary> Runs, bool HasMore);

public sealed record GitHubRunJob(
    string Name,
    GitHubRunStatus Status,
    GitHubRunConclusion? Conclusion,
    DateTimeOffset? StartedAtUtc,
    DateTimeOffset? CompletedAtUtc);

public sealed record GitHubRunJobs(IReadOnlyList<GitHubRunJob> Jobs);

/// <summary>
/// Reads a repository's automation and how it executed, for the Workflows screen.
/// </summary>
/// <remarks>
/// <para>
/// Separate from <see cref="IGitHubRepositoryCatalog"/> on purpose. That port serves registration, where a
/// workflow is a name to pick from a list and one collapsed outcome is enough. This screen needs status and
/// conclusion apart, the trigger, the actor, timings, and whether manual dispatch exists. The two should
/// converge on one read once this one is proven, and the backlog records that.
/// </para>
/// <para>
/// Read-only. Nothing here starts, cancels, or changes a workflow.
/// </para>
/// </remarks>
public interface IGitHubWorkflowInventory
{
    /// <summary>
    /// Lists a repository's workflows, each with its most recent run.
    /// </summary>
    /// <remarks>
    /// Costs one request for the workflow list plus one per workflow for its latest run. The alternative - one
    /// bounded page of recent runs across the repository - is cheaper but cannot tell "never run" from "ran
    /// before the page we read", and reporting the first as the second would be a fabrication.
    /// </remarks>
    Task<GitHubFactResult<GitHubWorkflowInventoryPage>> ListWorkflowsAsync(
        string owner,
        string repository,
        CancellationToken cancellationToken);

    /// <summary>
    /// Lists a workflow's recent runs, newest first.
    /// </summary>
    /// <remarks>
    /// Bounded to one page: run history answers "what has this been doing lately", and a caller that wanted the
    /// whole history would be asking the provider to page through years of runs.
    /// </remarks>
    Task<GitHubFactResult<GitHubRunPage>> ListRunsAsync(
        string owner,
        string repository,
        long workflowId,
        int limit,
        CancellationToken cancellationToken);

    /// <summary>
    /// Lists the jobs of one run, read on demand for the workflow an operator selected.
    /// </summary>
    Task<GitHubFactResult<GitHubRunJobs>> ListRunJobsAsync(
        string owner,
        string repository,
        long runId,
        CancellationToken cancellationToken);
}
