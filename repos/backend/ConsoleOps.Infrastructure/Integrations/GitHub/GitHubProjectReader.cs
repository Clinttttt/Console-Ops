using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.Json.Serialization;
using ConsoleOps.Application.Integrations.GitHub;

namespace ConsoleOps.Infrastructure.Integrations.GitHub;

public sealed class GitHubProjectReader(HttpClient httpClient, TimeProvider timeProvider)
    : IGitHubProjectReader
{
    internal const string ApiVersion = "2026-03-10";
    internal const string UserAgent = "ConsoleOps/1.0";

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
        Task<GitHubFactResult<GitHubWorkflowObservation>> workflowTask =
            ReadWorkflowAsync(project, cancellationToken);

        await Task.WhenAll(sourceTask, workflowTask);
        GitHubFactResult<GitHubSourceObservation> source = await sourceTask;
        IReadOnlyList<GitHubCommitComparison> comparisons = source.Observation is null
            ? []
            : await ReadComparisonsAsync(
                project,
                source.Observation.CommitSha,
                deployedCommitShas,
                cancellationToken);

        return new GitHubProjectReadResult(
            source,
            await workflowTask,
            comparisons);
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
        GitHubResponse<GitHubComparisonDto> response =
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
        GitHubResponse<GitHubCommitDto[]> response =
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

    private async Task<GitHubFactResult<GitHubWorkflowObservation>> ReadWorkflowAsync(
        GitHubProjectReference project,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(project.WorkflowFile))
        {
            return GitHubFactResult<GitHubWorkflowObservation>.Success(
                new GitHubWorkflowObservation(
                    null,
                    null,
                    GitHubWorkflowState.NotConfigured,
                    null,
                    null,
                    null,
                    timeProvider.GetUtcNow()));
        }

        string workflowFile = project.WorkflowFile.Trim();
        string path = $"repos/{Escape(project.Owner)}/{Escape(project.Repository)}"
            + $"/actions/workflows/{Escape(workflowFile)}/runs"
            + $"?branch={Escape(project.DefaultBranch)}&per_page=1";
        GitHubResponse<GitHubWorkflowRunsDto> response =
            await GetAsync<GitHubWorkflowRunsDto>(path, cancellationToken);

        if (response.Failure is not null)
        {
            return GitHubFactResult<GitHubWorkflowObservation>.Failed(response.Failure.Value);
        }

        GitHubWorkflowRunDto? run = response.Value?.WorkflowRuns?.FirstOrDefault();
        DateTimeOffset observedAtUtc = timeProvider.GetUtcNow();

        if (run is null)
        {
            return GitHubFactResult<GitHubWorkflowObservation>.Success(
                new GitHubWorkflowObservation(
                    workflowFile,
                    null,
                    GitHubWorkflowState.Unknown,
                    null,
                    null,
                    null,
                    observedAtUtc));
        }

        GitHubWorkflowState state = MapWorkflowState(run.Status, run.Conclusion);
        DateTimeOffset? completedAtUtc = IsTerminal(state) ? run.UpdatedAt : null;

        return GitHubFactResult<GitHubWorkflowObservation>.Success(
            new GitHubWorkflowObservation(
                workflowFile,
                NullIfWhiteSpace(run.Name),
                state,
                IsFullCommitSha(run.HeadSha) ? run.HeadSha : null,
                run.RunStartedAt,
                completedAtUtc,
                observedAtUtc));
    }

    private async Task<GitHubResponse<T>> GetAsync<T>(
        string relativePath,
        CancellationToken cancellationToken)
        where T : class
    {
        try
        {
            using HttpRequestMessage request = new(HttpMethod.Get, relativePath);
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));
            request.Headers.UserAgent.ParseAdd(UserAgent);
            request.Headers.Add("X-GitHub-Api-Version", ApiVersion);

            using HttpResponseMessage response = await httpClient.SendAsync(
                request,
                HttpCompletionOption.ResponseHeadersRead,
                cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                return GitHubResponse<T>.Failed(MapFailure(response));
            }

            await using Stream stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            T? value = await JsonSerializer.DeserializeAsync<T>(
                stream,
                SerializerOptions,
                cancellationToken);

            return value is null
                ? GitHubResponse<T>.Failed(GitHubReadFailure.InvalidResponse)
                : GitHubResponse<T>.Success(value);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return GitHubResponse<T>.Failed(GitHubReadFailure.Unavailable);
        }
        catch (HttpRequestException)
        {
            return GitHubResponse<T>.Failed(GitHubReadFailure.Unavailable);
        }
        catch (JsonException)
        {
            return GitHubResponse<T>.Failed(GitHubReadFailure.InvalidResponse);
        }
        catch (NotSupportedException)
        {
            return GitHubResponse<T>.Failed(GitHubReadFailure.InvalidResponse);
        }
    }

    private static GitHubReadFailure MapFailure(HttpResponseMessage response)
    {
        if (response.StatusCode == HttpStatusCode.TooManyRequests
            || response.Headers.TryGetValues("X-RateLimit-Remaining", out IEnumerable<string>? values)
            && values.Contains("0", StringComparer.Ordinal))
        {
            return GitHubReadFailure.RateLimited;
        }

        return response.StatusCode switch
        {
            HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden => GitHubReadFailure.Unauthorized,
            HttpStatusCode.NotFound => GitHubReadFailure.NotFound,
            >= HttpStatusCode.InternalServerError => GitHubReadFailure.Unavailable,
            _ => GitHubReadFailure.InvalidResponse
        };
    }

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

    private sealed record GitHubResponse<T>(T? Value, GitHubReadFailure? Failure)
        where T : class
    {
        public static GitHubResponse<T> Success(T value) => new(value, null);

        public static GitHubResponse<T> Failed(GitHubReadFailure failure) => new(null, failure);
    }

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
        string? Name,
        string? Status,
        string? Conclusion,
        [property: JsonPropertyName("head_sha")]
        string? HeadSha,
        [property: JsonPropertyName("run_started_at")]
        DateTimeOffset? RunStartedAt,
        [property: JsonPropertyName("updated_at")]
        DateTimeOffset? UpdatedAt);

    private sealed record GitHubComparisonDto(
        string? Status,
        [property: JsonPropertyName("ahead_by")]
        int AheadBy);
}
