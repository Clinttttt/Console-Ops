using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.Json.Serialization;
using ConsoleOps.Application.Integrations.GitHub;

namespace ConsoleOps.Infrastructure.Integrations.GitHub;

/// <summary>
/// Reads the repositories and workflows the configured GitHub credential can see.
/// </summary>
/// <remarks>
/// Provider DTOs stay inside this adapter. Nothing is persisted and no credential is ever returned:
/// the response carries only names, branches, paths and run outcomes.
///
/// The request and failure handling mirrors <see cref="GitHubProjectReader"/>. When a third caller
/// appears, lift the shared request helper out of both rather than copying it again.
/// </remarks>
public sealed class GitHubRepositoryCatalog(HttpClient httpClient) : IGitHubRepositoryCatalog
{
    /// <summary>GitHub's maximum page size, so one request covers most accounts.</summary>
    private const int PageSize = 100;

    /// <summary>Upper bound on what the picker shows, independent of GitHub's page size.</summary>
    private const int ResultLimit = 30;

    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    public async Task<GitHubFactResult<GitHubRepositoryCatalogPage>> ListRepositoriesAsync(
        string? query,
        CancellationToken cancellationToken)
    {
        // Sorted by push time so the repositories the operator is working in appear first.
        string path = $"user/repos?per_page={PageSize}&sort=pushed&direction=desc";
        GitHubReadResponse<GitHubRepositoryDto[]> response =
            await GetAsync<GitHubRepositoryDto[]>(path, cancellationToken);

        if (response.Value is null)
        {
            return GitHubFactResult<GitHubRepositoryCatalogPage>.Failed(
                response.Failure ?? GitHubReadFailure.Unavailable);
        }

        string? filter = NullIfWhiteSpace(query);
        GitHubRepositorySummary[] matches = response.Value
            .Where(repository => IsUsable(repository))
            .Select(ToSummary)
            .Where(repository => Matches(repository, filter))
            .ToArray();

        GitHubRepositorySummary[] page = matches.Take(ResultLimit).ToArray();
        bool hasMore = matches.Length > page.Length || response.HasNextPage;

        return GitHubFactResult<GitHubRepositoryCatalogPage>.Success(
            new GitHubRepositoryCatalogPage(page, hasMore));
    }

    public async Task<GitHubFactResult<GitHubWorkflowCatalog>> ListWorkflowsAsync(
        string owner,
        string repository,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(owner);
        ArgumentException.ThrowIfNullOrWhiteSpace(repository);

        string path = $"repos/{Escape(owner)}/{Escape(repository)}/actions/workflows?per_page={PageSize}";
        GitHubReadResponse<GitHubWorkflowsDto> response =
            await GetAsync<GitHubWorkflowsDto>(path, cancellationToken);

        if (response.Value is null)
        {
            return GitHubFactResult<GitHubWorkflowCatalog>.Failed(
                response.Failure ?? GitHubReadFailure.Unavailable);
        }

        GitHubWorkflowDto[] workflows = response.Value.Workflows ?? [];
        List<GitHubWorkflowSummary> summaries = new(workflows.Length);

        foreach (GitHubWorkflowDto workflow in workflows)
        {
            string? name = NullIfWhiteSpace(workflow.Name);
            string? workflowPath = NullIfWhiteSpace(workflow.Path);
            if (name is null || workflowPath is null)
            {
                continue;
            }

            GitHubLatestRun latestRun = await ReadLatestRunAsync(
                owner,
                repository,
                workflowPath,
                cancellationToken);

            summaries.Add(new GitHubWorkflowSummary(
                name,
                workflowPath,
                FileNameOf(workflowPath),
                IsActive(workflow.State),
                latestRun.Conclusion,
                latestRun.CompletedAtUtc));
        }

        return GitHubFactResult<GitHubWorkflowCatalog>.Success(new GitHubWorkflowCatalog(summaries));
    }

    /// <summary>
    /// Reads the most recent run so the operator can recognise a workflow by its last outcome.
    /// A failure here is not fatal: the workflow is still listed with an unknown conclusion.
    /// </summary>
    private async Task<GitHubLatestRun> ReadLatestRunAsync(
        string owner,
        string repository,
        string workflowPath,
        CancellationToken cancellationToken)
    {
        string path = $"repos/{Escape(owner)}/{Escape(repository)}"
            + $"/actions/workflows/{Escape(FileNameOf(workflowPath))}/runs?per_page=1";
        GitHubReadResponse<GitHubWorkflowRunsDto> response =
            await GetAsync<GitHubWorkflowRunsDto>(path, cancellationToken);

        GitHubWorkflowRunDto? run = response.Value?.WorkflowRuns?.FirstOrDefault();
        if (run is null)
        {
            return new GitHubLatestRun(
                response.Value is null
                    ? GitHubWorkflowRunConclusion.Unknown
                    : GitHubWorkflowRunConclusion.Never,
                null);
        }

        GitHubWorkflowRunConclusion conclusion = MapConclusion(run.Status, run.Conclusion);
        DateTimeOffset? completedAtUtc = conclusion is GitHubWorkflowRunConclusion.Success
            or GitHubWorkflowRunConclusion.Failure
            or GitHubWorkflowRunConclusion.Cancelled
            ? run.UpdatedAt
            : null;

        return new GitHubLatestRun(conclusion, completedAtUtc);
    }

    private async Task<GitHubReadResponse<T>> GetAsync<T>(
        string relativePath,
        CancellationToken cancellationToken)
        where T : class
    {
        try
        {
            using HttpRequestMessage request = new(HttpMethod.Get, relativePath);
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));
            request.Headers.UserAgent.ParseAdd(GitHubProjectReader.UserAgent);
            request.Headers.Add("X-GitHub-Api-Version", GitHubProjectReader.ApiVersion);

            using HttpResponseMessage response = await httpClient.SendAsync(
                request,
                HttpCompletionOption.ResponseHeadersRead,
                cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                return GitHubReadResponse<T>.Failed(MapFailure(response));
            }

            await using Stream stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            T? value = await JsonSerializer.DeserializeAsync<T>(
                stream,
                SerializerOptions,
                cancellationToken);

            return value is null
                ? GitHubReadResponse<T>.Failed(GitHubReadFailure.InvalidResponse)
                : GitHubReadResponse<T>.Success(value, HasNextPage(response));
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return GitHubReadResponse<T>.Failed(GitHubReadFailure.Unavailable);
        }
        catch (HttpRequestException)
        {
            return GitHubReadResponse<T>.Failed(GitHubReadFailure.Unavailable);
        }
        catch (JsonException)
        {
            return GitHubReadResponse<T>.Failed(GitHubReadFailure.InvalidResponse);
        }
        catch (NotSupportedException)
        {
            return GitHubReadResponse<T>.Failed(GitHubReadFailure.InvalidResponse);
        }
    }

    private static bool HasNextPage(HttpResponseMessage response) =>
        response.Headers.TryGetValues("Link", out IEnumerable<string>? links)
        && links.Any(link => link.Contains("rel=\"next\"", StringComparison.OrdinalIgnoreCase));

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

    private static GitHubWorkflowRunConclusion MapConclusion(string? status, string? conclusion) =>
        status?.Trim().ToLowerInvariant() switch
        {
            "queued" or "waiting" or "pending" or "requested" or "in_progress" =>
                GitHubWorkflowRunConclusion.InProgress,
            "completed" => conclusion?.Trim().ToLowerInvariant() switch
            {
                "success" => GitHubWorkflowRunConclusion.Success,
                "failure" or "timed_out" or "action_required" or "startup_failure" =>
                    GitHubWorkflowRunConclusion.Failure,
                "cancelled" => GitHubWorkflowRunConclusion.Cancelled,
                _ => GitHubWorkflowRunConclusion.Unknown
            },
            _ => GitHubWorkflowRunConclusion.Unknown
        };

    private static bool IsUsable(GitHubRepositoryDto repository) =>
        !string.IsNullOrWhiteSpace(repository.Name)
        && !string.IsNullOrWhiteSpace(repository.Owner?.Login)
        && !string.IsNullOrWhiteSpace(repository.DefaultBranch);

    private static GitHubRepositorySummary ToSummary(GitHubRepositoryDto repository) =>
        new(
            repository.Owner!.Login!.Trim(),
            repository.Name!.Trim(),
            repository.DefaultBranch!.Trim(),
            repository.Private,
            NullIfWhiteSpace(repository.Language),
            repository.PushedAt,
            NullIfWhiteSpace(repository.HtmlUrl));

    private static bool Matches(GitHubRepositorySummary repository, string? filter) =>
        filter is null
        || repository.Name.Contains(filter, StringComparison.OrdinalIgnoreCase)
        || repository.Owner.Contains(filter, StringComparison.OrdinalIgnoreCase);

    private static bool IsActive(string? state) =>
        !string.Equals(state?.Trim(), "disabled_manually", StringComparison.OrdinalIgnoreCase)
        && !string.Equals(state?.Trim(), "disabled_inactivity", StringComparison.OrdinalIgnoreCase);

    private static string FileNameOf(string path)
    {
        int separator = path.LastIndexOf('/');
        return separator < 0 ? path : path[(separator + 1)..];
    }

    private static string Escape(string value) => Uri.EscapeDataString(value.Trim());

    private static string? NullIfWhiteSpace(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private sealed record GitHubLatestRun(
        GitHubWorkflowRunConclusion Conclusion,
        DateTimeOffset? CompletedAtUtc);

    private sealed record GitHubReadResponse<T>(T? Value, GitHubReadFailure? Failure, bool HasNextPage)
        where T : class
    {
        public static GitHubReadResponse<T> Success(T value, bool hasNextPage) =>
            new(value, null, hasNextPage);

        public static GitHubReadResponse<T> Failed(GitHubReadFailure failure) =>
            new(null, failure, false);
    }

    private sealed record GitHubRepositoryDto(
        string? Name,
        GitHubOwnerDto? Owner,
        [property: JsonPropertyName("default_branch")]
        string? DefaultBranch,
        bool Private,
        string? Language,
        [property: JsonPropertyName("pushed_at")]
        DateTimeOffset? PushedAt,
        [property: JsonPropertyName("html_url")]
        string? HtmlUrl);

    private sealed record GitHubOwnerDto(string? Login);

    private sealed record GitHubWorkflowsDto(GitHubWorkflowDto[]? Workflows);

    private sealed record GitHubWorkflowDto(string? Name, string? Path, string? State);

    private sealed record GitHubWorkflowRunsDto(
        [property: JsonPropertyName("workflow_runs")]
        GitHubWorkflowRunDto[]? WorkflowRuns);

    private sealed record GitHubWorkflowRunDto(
        string? Status,
        string? Conclusion,
        [property: JsonPropertyName("updated_at")]
        DateTimeOffset? UpdatedAt);
}
