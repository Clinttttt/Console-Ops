using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.Json.Serialization;
using ConsoleOps.Application.Integrations.GitHub;

namespace ConsoleOps.Infrastructure.Integrations.GitHub;

public sealed class GitHubProjectReader(HttpClient httpClient, TimeProvider timeProvider)
    : IGitHubProjectReader
{
    internal const string ApiVersion = GitHubRead.ApiVersion;
    internal const string UserAgent = GitHubRead.UserAgent;

    /// <summary>
    /// How many recent runs of the configured workflow one refresh records. The newest run is also the
    /// project's current workflow state, so release history costs no extra GitHub request. Bounded on
    /// purpose: refresh is interactive and a project's history is filled in over successive refreshes.
    /// </summary>
    internal const int WorkflowRunPageSize = 20;

    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    public async Task<GitHubProjectReadResult> ReadAsync(
        GitHubProjectReference project,
        IReadOnlyCollection<string> deployedCommitShas,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(project);
        ArgumentNullException.ThrowIfNull(deployedCommitShas);
        ArgumentException.ThrowIfNullOrWhiteSpace(project.Owner);
        ArgumentException.ThrowIfNullOrWhiteSpace(project.Repository);
        ArgumentException.ThrowIfNullOrWhiteSpace(project.DefaultBranch);

        Task<GitHubFactResult<GitHubSourceObservation>> sourceTask =
            ReadSourceAsync(project, cancellationToken);
        Task<WorkflowReadResult> workflowTask =
            ReadWorkflowAsync(project, cancellationToken);

        await Task.WhenAll(sourceTask, workflowTask);
        GitHubFactResult<GitHubSourceObservation> source = await sourceTask;
        WorkflowReadResult workflow = await workflowTask;
        IReadOnlyList<GitHubCommitComparison> comparisons = source.Observation is null
            ? []
            : await ReadComparisonsAsync(
                project,
                source.Observation.CommitSha,
                deployedCommitShas,
                cancellationToken);

        return new GitHubProjectReadResult(
            source,
            workflow.Observation,
            comparisons,
            workflow.Runs);
    }

    private async Task<IReadOnlyList<GitHubCommitComparison>> ReadComparisonsAsync(
        GitHubProjectReference project,
        string sourceCommitSha,
        IReadOnlyCollection<string> deployedCommitShas,
        CancellationToken cancellationToken)
    {
        string[] commits = deployedCommitShas
            .Where(IsFullCommitSha)
            .Where(commit => !string.Equals(commit, sourceCommitSha, StringComparison.OrdinalIgnoreCase))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        GitHubCommitComparison[] comparisons = new GitHubCommitComparison[commits.Length];

        await Parallel.ForEachAsync(
            Enumerable.Range(0, commits.Length),
            new ParallelOptions
            {
                MaxDegreeOfParallelism = 4,
                CancellationToken = cancellationToken
            },
            async (index, token) =>
            {
                comparisons[index] = await ReadComparisonAsync(
                    project,
                    commits[index],
                    sourceCommitSha,
                    token);
            });

        return comparisons;
    }

    private async Task<GitHubCommitComparison> ReadComparisonAsync(
        GitHubProjectReference project,
        string deployedCommitSha,
        string sourceCommitSha,
        CancellationToken cancellationToken)
    {
        string path = $"repos/{Escape(project.Owner)}/{Escape(project.Repository)}"
            + $"/compare/{Escape(deployedCommitSha)}...{Escape(sourceCommitSha)}?per_page=1";
        GitHubReadResponse<GitHubComparisonDto> response =
            await GetAsync<GitHubComparisonDto>(path, cancellationToken);
        DateTimeOffset observedAtUtc = timeProvider.GetUtcNow();

        if (response.Failure is not null)
        {
            return new GitHubCommitComparison(
                deployedCommitSha,
                sourceCommitSha,
                GitHubCommitRelation.Unknown,
                null,
                response.Failure,
                observedAtUtc);
        }

        GitHubCommitRelation relation = response.Value?.Status?.Trim().ToLowerInvariant() switch
        {
            "ahead" when response.Value.AheadBy > 0 => GitHubCommitRelation.DeployedIsAncestor,
            "identical" => GitHubCommitRelation.Identical,
            _ => GitHubCommitRelation.Unknown
        };

        return new GitHubCommitComparison(
            deployedCommitSha,
            sourceCommitSha,
            relation,
            relation == GitHubCommitRelation.DeployedIsAncestor ? response.Value?.AheadBy : null,
            null,
            observedAtUtc);
    }

    private async Task<GitHubFactResult<GitHubSourceObservation>> ReadSourceAsync(
        GitHubProjectReference project,
        CancellationToken cancellationToken)
    {
        string path = $"repos/{Escape(project.Owner)}/{Escape(project.Repository)}/commits"
            + $"?sha={Escape(project.DefaultBranch)}&per_page=1";
        GitHubReadResponse<GitHubCommitDto[]> response =
            await GetAsync<GitHubCommitDto[]>(path, cancellationToken);

        if (response.Failure is not null)
        {
            return GitHubFactResult<GitHubSourceObservation>.Failed(response.Failure.Value);
        }

        GitHubCommitDto? commit = response.Value?.FirstOrDefault();
        if (commit is null || !IsFullCommitSha(commit.Sha))
        {
            return GitHubFactResult<GitHubSourceObservation>.Failed(GitHubReadFailure.InvalidResponse);
        }

        string commitSha = commit.Sha!;
        DateTimeOffset? committedAtUtc = commit.Commit?.Committer?.Date
            ?? commit.Commit?.Author?.Date;
        DateTimeOffset observedAtUtc = timeProvider.GetUtcNow();

        return GitHubFactResult<GitHubSourceObservation>.Success(
            new GitHubSourceObservation(
                $"{project.Owner}/{project.Repository}",
                project.DefaultBranch,
                commitSha,
                commitSha[..7],
                committedAtUtc,
                observedAtUtc));
    }

    private async Task<WorkflowReadResult> ReadWorkflowAsync(
        GitHubProjectReference project,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(project.WorkflowFile))
        {
            return new WorkflowReadResult(
                GitHubFactResult<GitHubWorkflowObservation>.Success(
                    new GitHubWorkflowObservation(
                        null,
                        null,
                        GitHubWorkflowState.NotConfigured,
                        null,
                        null,
                        null,
                        timeProvider.GetUtcNow())),
                []);
        }

        string workflowFile = project.WorkflowFile.Trim();
        string path = $"repos/{Escape(project.Owner)}/{Escape(project.Repository)}"
            + $"/actions/workflows/{Escape(workflowFile)}/runs"
            + $"?branch={Escape(project.DefaultBranch)}&per_page={WorkflowRunPageSize}";
        GitHubReadResponse<GitHubWorkflowRunsDto> response =
            await GetAsync<GitHubWorkflowRunsDto>(path, cancellationToken);

        if (response.Failure is not null)
        {
            return new WorkflowReadResult(
                GitHubFactResult<GitHubWorkflowObservation>.Failed(response.Failure.Value),
                []);
        }

        GitHubWorkflowRunDto[] runs = response.Value?.WorkflowRuns ?? [];
        GitHubWorkflowRunDto? run = runs.FirstOrDefault();
        DateTimeOffset observedAtUtc = timeProvider.GetUtcNow();

        if (run is null)
        {
            return new WorkflowReadResult(
                GitHubFactResult<GitHubWorkflowObservation>.Success(
                    new GitHubWorkflowObservation(
                        workflowFile,
                        null,
                        GitHubWorkflowState.Unknown,
                        null,
                        null,
                        null,
                        observedAtUtc)),
                []);
        }

        GitHubWorkflowState state = MapWorkflowState(run.Status, run.Conclusion);
        DateTimeOffset? completedAtUtc = IsTerminal(state) ? run.UpdatedAt : null;

        return new WorkflowReadResult(
            GitHubFactResult<GitHubWorkflowObservation>.Success(
                new GitHubWorkflowObservation(
                    workflowFile,
                    NullIfWhiteSpace(run.Name),
                    state,
                    IsFullCommitSha(run.HeadSha) ? run.HeadSha : null,
                    run.RunStartedAt,
                    completedAtUtc,
                    observedAtUtc)),
            MapRuns(project, workflowFile, runs, observedAtUtc));
    }

    /// <summary>
    /// Turns the run page into release records, keeping only runs whose identity can be trusted.
    /// A run without a numeric id or a full commit SHA cannot be reconciled with a deployed version
    /// later, so it is dropped rather than recorded as a partly-known release.
    /// </summary>
    private static IReadOnlyList<GitHubWorkflowRun> MapRuns(
        GitHubProjectReference project,
        string workflowFile,
        IReadOnlyList<GitHubWorkflowRunDto> runs,
        DateTimeOffset observedAtUtc)
    {
        List<GitHubWorkflowRun> mapped = new(runs.Count);

        foreach (GitHubWorkflowRunDto run in runs)
        {
            if (run.Id is not long runId || runId <= 0 || !IsFullCommitSha(run.HeadSha))
            {
                continue;
            }

            GitHubWorkflowState state = MapWorkflowState(run.Status, run.Conclusion);
            mapped.Add(new GitHubWorkflowRun(
                runId,
                run.RunNumber,
                workflowFile,
                NullIfWhiteSpace(run.Name),
                NullIfWhiteSpace(run.HeadBranch) ?? project.DefaultBranch.Trim(),
                run.HeadSha!,
                state,
                run.RunStartedAt ?? run.CreatedAt,
                IsTerminal(state) ? run.UpdatedAt : null,
                NullIfWhiteSpace(run.Actor?.Login),
                SafeRunUrl(run.HtmlUrl),
                observedAtUtc));
        }

        return mapped;
    }

    /// <summary>
    /// Accepts a run link only when it is an absolute GitHub HTTPS URL without embedded credentials,
    /// because the browser renders it as an outbound link.
    /// </summary>
    private static string? SafeRunUrl(string? value)
    {
        if (!Uri.TryCreate(value, UriKind.Absolute, out Uri? url)
            || url.Scheme != Uri.UriSchemeHttps
            || !string.IsNullOrEmpty(url.UserInfo))
        {
            return null;
        }

        string host = url.Host;
        bool isGitHubHost = host.Equals("github.com", StringComparison.OrdinalIgnoreCase)
            || host.EndsWith(".github.com", StringComparison.OrdinalIgnoreCase);

        return isGitHubHost ? url.AbsoluteUri : null;
    }

    /// <summary>Every read goes through the shared GitHub request, so failure means the same thing here.</summary>
    private Task<GitHubReadResponse<T>> GetAsync<T>(
        string relativePath,
        CancellationToken cancellationToken)
        where T : class =>
        GitHubRead.GetAsync<T>(httpClient, relativePath, cancellationToken);
    private static GitHubWorkflowState MapWorkflowState(string? status, string? conclusion)
    {
        string normalizedStatus = status?.Trim().ToLowerInvariant() ?? string.Empty;

        return normalizedStatus switch
        {
            "queued" or "waiting" or "pending" or "requested" => GitHubWorkflowState.Queued,
            "in_progress" => GitHubWorkflowState.InProgress,
            "completed" => MapConclusion(conclusion),
            _ => GitHubWorkflowState.Unknown
        };
    }

    private static GitHubWorkflowState MapConclusion(string? conclusion) =>
        conclusion?.Trim().ToLowerInvariant() switch
        {
            "success" => GitHubWorkflowState.Passed,
            "failure" or "timed_out" or "action_required" or "startup_failure" =>
                GitHubWorkflowState.Failed,
            "cancelled" => GitHubWorkflowState.Cancelled,
            _ => GitHubWorkflowState.Unknown
        };

    private static bool IsTerminal(GitHubWorkflowState state) =>
        state is GitHubWorkflowState.Passed
            or GitHubWorkflowState.Failed
            or GitHubWorkflowState.Cancelled;

    private static bool IsFullCommitSha(string? value) =>
        value is { Length: 40 or 64 } && value.All(Uri.IsHexDigit);

    private static string Escape(string value) => Uri.EscapeDataString(value.Trim());

    private static string? NullIfWhiteSpace(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();


    private sealed record GitHubCommitDto(
        string? Sha,
        GitHubCommitDetailsDto? Commit);

    private sealed record GitHubCommitDetailsDto(
        GitHubCommitPersonDto? Author,
        GitHubCommitPersonDto? Committer);

    private sealed record GitHubCommitPersonDto(DateTimeOffset? Date);

    private sealed record GitHubWorkflowRunsDto(
        [property: JsonPropertyName("workflow_runs")]
        GitHubWorkflowRunDto[]? WorkflowRuns);

    private sealed record GitHubWorkflowRunDto(
        long? Id,
        string? Name,
        string? Status,
        string? Conclusion,
        [property: JsonPropertyName("run_number")]
        int? RunNumber,
        [property: JsonPropertyName("head_sha")]
        string? HeadSha,
        [property: JsonPropertyName("head_branch")]
        string? HeadBranch,
        [property: JsonPropertyName("html_url")]
        string? HtmlUrl,
        GitHubActorDto? Actor,
        [property: JsonPropertyName("run_started_at")]
        DateTimeOffset? RunStartedAt,
        [property: JsonPropertyName("created_at")]
        DateTimeOffset? CreatedAt,
        [property: JsonPropertyName("updated_at")]
        DateTimeOffset? UpdatedAt);

    private sealed record GitHubActorDto(string? Login);

    private sealed record WorkflowReadResult(
        GitHubFactResult<GitHubWorkflowObservation> Observation,
        IReadOnlyList<GitHubWorkflowRun> Runs);

    private sealed record GitHubComparisonDto(
        string? Status,
        [property: JsonPropertyName("ahead_by")]
        int AheadBy);
}
