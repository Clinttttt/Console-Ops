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

/// <param name="Number">
/// The step's position as the provider numbers it, so an unnamed step is still identifiable and the order is the
/// provider's rather than one this adapter invented.
/// </param>
public sealed record GitHubRunStep(
    string Name,
    int? Number,
    GitHubRunStatus Status,
    GitHubRunConclusion? Conclusion,
    DateTimeOffset? StartedAtUtc,
    DateTimeOffset? CompletedAtUtc);

/// <param name="Steps">
/// The job's steps, in provider order. Empty where the provider reported none - which happens while a job is
/// still queued, and is not the same as a job that ran nothing.
/// </param>
public sealed record GitHubRunJob(
    string Name,
    GitHubRunStatus Status,
    GitHubRunConclusion? Conclusion,
    DateTimeOffset? StartedAtUtc,
    DateTimeOffset? CompletedAtUtc,
    IReadOnlyList<GitHubRunStep> Steps);

public sealed record GitHubRunJobs(IReadOnlyList<GitHubRunJob> Jobs);

/// <param name="Type">
/// The provider's own input type: <c>choice</c>, <c>boolean</c>, <c>environment</c>, or <c>string</c>. Carried so
/// a form renders what the workflow declared rather than a text box for everything.
/// </param>
/// <param name="Options">Allowed values where the workflow declared them, otherwise empty.</param>
public sealed record GitHubWorkflowInput(
    string Name,
    string? Description,
    bool Required,
    string Type,
    string? Default,
    IReadOnlyList<string> Options);

/// <param name="SupportsManualRun">
/// <c>true</c> or <c>false</c> where the workflow's trigger declaration was read, and <c>null</c> where it was
/// not. Unknown is a real answer: it means Console Ops could not establish the fact, which is different from
/// establishing that a manual run is unavailable.
/// </param>
/// <param name="DefinitionPath">The file the answer was read from, so a claim can be checked.</param>
/// <param name="Inputs">
/// What the workflow asks for when it is dispatched, in declaration order. Empty when it asks for nothing, which
/// is different from not knowing - an unread definition reports <c>null</c> support and no inputs.
/// </param>
public sealed record GitHubManualRunSupport(
    bool? SupportsManualRun,
    string DefinitionPath,
    IReadOnlyList<GitHubWorkflowInput> Inputs);

/// <summary>How a dispatch request ended. The provider reports acceptance, never a run.</summary>
public enum GitHubDispatchOutcome
{
    /// <summary>The provider accepted the request. A run may not exist yet.</summary>
    Accepted,

    /// <summary>The credential cannot start workflows in this repository.</summary>
    Forbidden,

    /// <summary>The workflow or ref does not exist as far as the provider is concerned.</summary>
    NotFound,

    /// <summary>The provider rejected the inputs or the ref as unusable.</summary>
    Rejected,

    RateLimited,

    Unavailable
}

/// <param name="ProviderMessage">
/// What the provider said, when it said anything.
/// </param>
/// <remarks>
/// Carried because a rejected dispatch is the one failure Console Ops cannot explain on its own: GitHub knows
/// whether the ref was wrong, the trigger was missing on that ref, or an input was not accepted, and repeating a
/// guess in place of its answer sends an operator through all three.
/// </remarks>
public sealed record GitHubDispatchResult(GitHubDispatchOutcome Outcome, string? ProviderMessage);

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
    /// Whether one workflow declares a manual dispatch trigger, read from its own definition.
    /// </summary>
    /// <remarks>
    /// A separate read because the listing does not report triggers and the answer costs one request for the
    /// file. Made for the workflow an operator selected rather than for every workflow on a page.
    /// </remarks>
    Task<GitHubFactResult<GitHubManualRunSupport>> ReadManualRunSupportAsync(
        string owner,
        string repository,
        string workflowPath,
        CancellationToken cancellationToken);

    /// <summary>
    /// One workflow as the provider currently describes it, or a failure when it does not.
    /// </summary>
    /// <remarks>
    /// Read before a dispatch so the name and path used to authorise it are the provider's, not the caller's. A
    /// request that could name its own workflow could name a path whose risk marking is not the one that applies.
    /// </remarks>
    Task<GitHubFactResult<GitHubWorkflowDefinition>> GetWorkflowAsync(
        string owner,
        string repository,
        long workflowId,
        CancellationToken cancellationToken);

    /// <summary>
    /// Asks the provider to start a workflow on a ref.
    /// </summary>
    /// <remarks>
    /// The provider answers with acceptance and no run: there is no run id to return, so a caller must find the
    /// run afterwards rather than being told which one it started. Reporting a run here would be an invention.
    /// </remarks>
    Task<GitHubDispatchResult> DispatchAsync(
        string owner,
        string repository,
        long workflowId,
        string reference,
        IReadOnlyDictionary<string, string> inputs,
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
