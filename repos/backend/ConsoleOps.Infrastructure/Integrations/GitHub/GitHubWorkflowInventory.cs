using System.Text.Json.Serialization;
using ConsoleOps.Application.Integrations.GitHub;

namespace ConsoleOps.Infrastructure.Integrations.GitHub;

/// <summary>
/// Reads a repository's workflows and how they executed, through the GitHub Actions API.
/// </summary>
/// <remarks>
/// <para>
/// Provider DTOs stay inside this adapter. Status and conclusion are mapped to Console Ops' own values but are
/// never merged: GitHub reports them separately because a run that is still going has no outcome, and a value
/// this adapter does not recognise becomes unknown rather than being rounded to the nearest familiar one.
/// </para>
/// <para>
/// Read-only, and bounded: one page of workflows, and one run per workflow.
/// </para>
/// </remarks>
public sealed class GitHubWorkflowInventory(HttpClient httpClient) : IGitHubWorkflowInventory
{
    /// <summary>A repository's whole workflow directory fits comfortably in one page.</summary>
    private const int WorkflowPageSize = 100;

    /// <summary>Jobs of a single run. A run with more than this has other problems.</summary>
    private const int JobPageSize = 100;

    /// <summary>Upper bound on run history, whatever a caller asks for. History is recent, not exhaustive.</summary>
    private const int MaximumRunPageSize = 50;

    /// <summary>
    /// How many latest-run reads are in flight at once.
    /// </summary>
    /// <remarks>
    /// Small on purpose. It is enough to keep a page of workflows from being read one round trip at a time,
    /// and low enough that Console Ops never looks like a burst of traffic against a shared rate limit.
    /// </remarks>
    private const int RunReadConcurrency = 4;

    public async Task<GitHubFactResult<GitHubWorkflowInventoryPage>> ListWorkflowsAsync(
        string owner,
        string repository,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(owner);
        ArgumentException.ThrowIfNullOrWhiteSpace(repository);

        string path = $"repos/{Uri.EscapeDataString(owner)}/{Uri.EscapeDataString(repository)}"
            + $"/actions/workflows?per_page={WorkflowPageSize}";
        GitHubReadResponse<WorkflowsDto> workflows =
            await GitHubRead.GetAsync<WorkflowsDto>(httpClient, path, cancellationToken);

        if (workflows.Value?.Workflows is null)
        {
            return GitHubFactResult<GitHubWorkflowInventoryPage>.Failed(
                workflows.Failure ?? GitHubReadFailure.Unavailable);
        }

        WorkflowDto[] usable = workflows.Value.Workflows
            .Where(workflow => workflow.Id > 0 && !string.IsNullOrWhiteSpace(workflow.Name))
            .ToArray();

        // Read in small concurrent batches. Sequentially this cost one round trip per workflow, which measured
        // 12.3 seconds for twelve workflows across three repositories - slow enough that an operator would
        // watch a blank screen. The batch is deliberately small: these share one credential and one rate limit,
        // and a page of unbounded parallel requests is how a refresh turns into a rate-limit failure for every
        // other read.
        GitHubRunSummary?[] latestRuns = new GitHubRunSummary?[usable.Length];
        for (int offset = 0; offset < usable.Length; offset += RunReadConcurrency)
        {
            int size = Math.Min(RunReadConcurrency, usable.Length - offset);
            Task<GitHubRunSummary?>[] reads = new Task<GitHubRunSummary?>[size];
            for (int index = 0; index < size; index++)
            {
                reads[index] = ReadLatestRunAsync(
                    owner,
                    repository,
                    usable[offset + index].Id,
                    cancellationToken);
            }

            GitHubRunSummary?[] batch = await Task.WhenAll(reads);
            Array.Copy(batch, 0, latestRuns, offset, size);
        }

        List<GitHubWorkflowDefinition> definitions = new(usable.Length);
        for (int index = 0; index < usable.Length; index++)
        {
            WorkflowDto workflow = usable[index];
            definitions.Add(new GitHubWorkflowDefinition(
                workflow.Id,
                workflow.Name!.Trim(),
                workflow.Path?.Trim() ?? string.Empty,
                IsActive(workflow.State),
                // Whether manual dispatch exists is in the workflow definition, not in this listing. Reading
                // and parsing every file to find out is a later slice, so it is reported as unknown rather
                // than assumed either way.
                SupportsManualRun: null,
                latestRuns[index]));
        }

        return GitHubFactResult<GitHubWorkflowInventoryPage>.Success(
            new GitHubWorkflowInventoryPage(definitions));
    }

    public async Task<GitHubFactResult<GitHubRunPage>> ListRunsAsync(
        string owner,
        string repository,
        long workflowId,
        int limit,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(owner);
        ArgumentException.ThrowIfNullOrWhiteSpace(repository);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(workflowId);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(limit);

        int pageSize = Math.Min(limit, MaximumRunPageSize);
        string path = $"repos/{Uri.EscapeDataString(owner)}/{Uri.EscapeDataString(repository)}"
            + $"/actions/workflows/{workflowId}/runs?per_page={pageSize}";
        GitHubReadResponse<RunsDto> response =
            await GitHubRead.GetAsync<RunsDto>(httpClient, path, cancellationToken);

        if (response.Value?.WorkflowRuns is null)
        {
            return GitHubFactResult<GitHubRunPage>.Failed(
                response.Failure ?? GitHubReadFailure.Unavailable);
        }

        GitHubRunSummary[] runs = response.Value.WorkflowRuns
            .Where(run => run.Id > 0)
            .Select(ToSummary)
            .ToArray();

        // GitHub reports the repository's total, which is how a screen can say this is recent history rather
        // than everything the workflow has ever done.
        bool hasMore = response.Value.TotalCount > runs.Length || response.HasNextPage;

        return GitHubFactResult<GitHubRunPage>.Success(new GitHubRunPage(runs, hasMore));
    }

    public async Task<GitHubFactResult<GitHubRunJobs>> ListRunJobsAsync(
        string owner,
        string repository,
        long runId,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(owner);
        ArgumentException.ThrowIfNullOrWhiteSpace(repository);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(runId);

        string path = $"repos/{Uri.EscapeDataString(owner)}/{Uri.EscapeDataString(repository)}"
            + $"/actions/runs/{runId}/jobs?per_page={JobPageSize}";
        GitHubReadResponse<JobsDto> response =
            await GitHubRead.GetAsync<JobsDto>(httpClient, path, cancellationToken);

        if (response.Value?.Jobs is null)
        {
            return GitHubFactResult<GitHubRunJobs>.Failed(
                response.Failure ?? GitHubReadFailure.Unavailable);
        }

        GitHubRunJob[] jobs = response.Value.Jobs
            .Where(job => !string.IsNullOrWhiteSpace(job.Name))
            .Select(job => new GitHubRunJob(
                job.Name!.Trim(),
                MapStatus(job.Status),
                MapConclusion(job.Conclusion),
                job.StartedAt,
                job.CompletedAt))
            .ToArray();

        return GitHubFactResult<GitHubRunJobs>.Success(new GitHubRunJobs(jobs));
    }

    /// <summary>
    /// The most recent run of one workflow, or <c>null</c> when GitHub reports none.
    /// </summary>
    /// <remarks>
    /// A failed read also yields <c>null</c>. The workflow itself was read successfully, so the honest report is
    /// a workflow whose latest run is not known - losing the whole inventory because one run could not be read
    /// would be worse, and the screen shows the absence rather than inventing a state.
    /// </remarks>
    private async Task<GitHubRunSummary?> ReadLatestRunAsync(
        string owner,
        string repository,
        long workflowId,
        CancellationToken cancellationToken)
    {
        string path = $"repos/{Uri.EscapeDataString(owner)}/{Uri.EscapeDataString(repository)}"
            + $"/actions/workflows/{workflowId}/runs?per_page=1";
        GitHubReadResponse<RunsDto> response =
            await GitHubRead.GetAsync<RunsDto>(httpClient, path, cancellationToken);

        RunDto? run = response.Value?.WorkflowRuns?.FirstOrDefault();
        return run is null || run.Id <= 0 ? null : ToSummary(run);
    }

    /// <summary>One run as Console Ops describes it, shared by the latest-run read and run history.</summary>
    private static GitHubRunSummary ToSummary(RunDto run)
    {
        GitHubRunStatus status = MapStatus(run.Status);

        return new GitHubRunSummary(
            run.Id,
            run.RunNumber,
            status,
            MapConclusion(run.Conclusion),
            run.HeadBranch?.Trim() ?? string.Empty,
            run.HeadSha?.Trim() ?? string.Empty,
            run.Event?.Trim() ?? string.Empty,
            NullIfWhiteSpace(run.Actor?.Login),
            run.RunStartedAt ?? run.CreatedAt,
            // GitHub reports no completion time for a run still going, and `updated_at` is only the end of a
            // run that has one.
            status == GitHubRunStatus.Completed ? run.UpdatedAt : null,
            NullIfWhiteSpace(run.HtmlUrl));
    }

    /// <summary>
    /// A workflow GitHub does not report as active is disabled, whether by an operator or by inactivity.
    /// </summary>
    private static bool IsActive(string? state) =>
        string.Equals(state?.Trim(), "active", StringComparison.OrdinalIgnoreCase);

    private static GitHubRunStatus MapStatus(string? status) =>
        status?.Trim().ToLowerInvariant() switch
        {
            "queued" or "requested" or "pending" => GitHubRunStatus.Queued,
            "in_progress" => GitHubRunStatus.InProgress,
            "waiting" => GitHubRunStatus.Waiting,
            "completed" => GitHubRunStatus.Completed,
            _ => GitHubRunStatus.Unknown
        };

    /// <summary>
    /// How a run ended, or <c>null</c> when it has not ended or reported something unrecognised.
    /// </summary>
    private static GitHubRunConclusion? MapConclusion(string? conclusion) =>
        conclusion?.Trim().ToLowerInvariant() switch
        {
            "success" => GitHubRunConclusion.Passed,
            "failure" or "startup_failure" => GitHubRunConclusion.Failed,
            "cancelled" => GitHubRunConclusion.Cancelled,
            "skipped" => GitHubRunConclusion.Skipped,
            "timed_out" => GitHubRunConclusion.TimedOut,
            "action_required" => GitHubRunConclusion.ActionRequired,
            "neutral" or "stale" => GitHubRunConclusion.Neutral,
            _ => null
        };

    private static string? NullIfWhiteSpace(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private sealed record WorkflowsDto(WorkflowDto[]? Workflows);

    private sealed record WorkflowDto(long Id, string? Name, string? Path, string? State);

    private sealed record RunsDto(
        [property: JsonPropertyName("total_count")] int TotalCount,
        [property: JsonPropertyName("workflow_runs")] RunDto[]? WorkflowRuns);

    private sealed record RunDto(
        long Id,
        [property: JsonPropertyName("run_number")] int? RunNumber,
        string? Status,
        string? Conclusion,
        [property: JsonPropertyName("head_branch")] string? HeadBranch,
        [property: JsonPropertyName("head_sha")] string? HeadSha,
        string? Event,
        ActorDto? Actor,
        [property: JsonPropertyName("created_at")] DateTimeOffset? CreatedAt,
        [property: JsonPropertyName("run_started_at")] DateTimeOffset? RunStartedAt,
        [property: JsonPropertyName("updated_at")] DateTimeOffset? UpdatedAt,
        [property: JsonPropertyName("html_url")] string? HtmlUrl);

    private sealed record ActorDto(string? Login);

    private sealed record JobsDto(JobDto[]? Jobs);

    private sealed record JobDto(
        string? Name,
        string? Status,
        string? Conclusion,
        [property: JsonPropertyName("started_at")] DateTimeOffset? StartedAt,
        [property: JsonPropertyName("completed_at")] DateTimeOffset? CompletedAt);
}
