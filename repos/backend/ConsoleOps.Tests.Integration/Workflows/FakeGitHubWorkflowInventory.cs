using ConsoleOps.Application.Integrations.GitHub;

namespace ConsoleOps.Tests.Integration.Workflows;

/// <summary>
/// A provider stand-in for the Workflows tests.
/// </summary>
/// <remarks>
/// One fake rather than an inline stub per test class, because both the inventory and the dispatch tests need the
/// same seam and two copies drifted the moment the port grew a method.
/// </remarks>
internal sealed class FakeGitHubWorkflowInventory : IGitHubWorkflowInventory
{
    private readonly GitHubWorkflowInventoryPage? page;
    private readonly GitHubReadFailure? listFailure;

    public FakeGitHubWorkflowInventory(GitHubWorkflowInventoryPage page) => this.page = page;

    public FakeGitHubWorkflowInventory(GitHubReadFailure failure) => listFailure = failure;

    /// <summary>What the provider says about one workflow, or a failure when set.</summary>
    public GitHubWorkflowDefinition? Workflow { get; set; }

    public GitHubReadFailure? WorkflowFailure { get; set; }

    /// <summary>Whether the definition declares a dispatch trigger, and what it asks for.</summary>
    public bool? SupportsManualRun { get; set; }

    public IReadOnlyList<GitHubWorkflowInput> Inputs { get; set; } = [];

    public GitHubReadFailure? SupportFailure { get; set; }

    public GitHubDispatchOutcome DispatchOutcome { get; set; } = GitHubDispatchOutcome.Accepted;

    /// <summary>What the provider said about a refusal, so a test can assert it reaches the caller.</summary>
    public string? DispatchMessage { get; set; }

    /// <summary>Every dispatch asked for, so a test can assert what was sent and what was not.</summary>
    public List<(string Owner, string Repository, long WorkflowId, string Reference,
        IReadOnlyDictionary<string, string> Inputs)> Dispatches { get; } = [];

    public Task<GitHubFactResult<GitHubWorkflowInventoryPage>> ListWorkflowsAsync(
        string owner,
        string repository,
        CancellationToken cancellationToken) =>
        Task.FromResult(page is null
            ? GitHubFactResult<GitHubWorkflowInventoryPage>.Failed(listFailure!.Value)
            : GitHubFactResult<GitHubWorkflowInventoryPage>.Success(page));

    public Task<GitHubFactResult<GitHubRunPage>> ListRunsAsync(
        string owner,
        string repository,
        long workflowId,
        int limit,
        CancellationToken cancellationToken) =>
        Task.FromResult(GitHubFactResult<GitHubRunPage>.Success(new GitHubRunPage([], false)));

    public Task<GitHubFactResult<GitHubManualRunSupport>> ReadManualRunSupportAsync(
        string owner,
        string repository,
        string workflowPath,
        CancellationToken cancellationToken) =>
        Task.FromResult(SupportFailure is null
            ? GitHubFactResult<GitHubManualRunSupport>.Success(
                new GitHubManualRunSupport(SupportsManualRun, workflowPath, Inputs))
            : GitHubFactResult<GitHubManualRunSupport>.Failed(SupportFailure.Value));

    public Task<GitHubFactResult<GitHubWorkflowDefinition>> GetWorkflowAsync(
        string owner,
        string repository,
        long workflowId,
        CancellationToken cancellationToken)
    {
        if (WorkflowFailure is not null)
        {
            return Task.FromResult(GitHubFactResult<GitHubWorkflowDefinition>.Failed(WorkflowFailure.Value));
        }

        GitHubWorkflowDefinition? match = Workflow
            ?? page?.Workflows.FirstOrDefault(workflow => workflow.WorkflowId == workflowId);

        return Task.FromResult(match is null
            ? GitHubFactResult<GitHubWorkflowDefinition>.Failed(GitHubReadFailure.NotFound)
            : GitHubFactResult<GitHubWorkflowDefinition>.Success(match));
    }

    public Task<GitHubDispatchResult> DispatchAsync(
        string owner,
        string repository,
        long workflowId,
        string reference,
        IReadOnlyDictionary<string, string> inputs,
        CancellationToken cancellationToken)
    {
        Dispatches.Add((owner, repository, workflowId, reference, inputs));
        return Task.FromResult(new GitHubDispatchResult(DispatchOutcome, DispatchMessage));
    }

    public Task<GitHubFactResult<GitHubRunJobs>> ListRunJobsAsync(
        string owner,
        string repository,
        long runId,
        CancellationToken cancellationToken) =>
        Task.FromResult(GitHubFactResult<GitHubRunJobs>.Success(new GitHubRunJobs([])));
}
