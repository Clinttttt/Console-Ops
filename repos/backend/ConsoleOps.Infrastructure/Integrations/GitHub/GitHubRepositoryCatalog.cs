using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
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

    /// <summary>Detection stays bounded: a few likely files, each of modest size.</summary>
    private const int MaximumInspectedFiles = 5;

    private const int MaximumFileBytes = 256 * 1024;

    private static readonly Regex HealthCheckPattern = new(
        @"MapHealthChecks\(\s*""(?<path>/[^""]*)""",
        RegexOptions.Compiled | RegexOptions.CultureInvariant,
        TimeSpan.FromSeconds(1));

    private static readonly Regex AppMapGetPattern = new(
        @"\bapp\s*\.\s*MapGet\(\s*""(?<path>/[^""]*)""",
        RegexOptions.Compiled | RegexOptions.CultureInvariant,
        TimeSpan.FromSeconds(1));

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

    public async Task<GitHubFactResult<GitHubLatestCommit>> GetLatestCommitAsync(
        string owner,
        string repository,
        string branch,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(owner);
        ArgumentException.ThrowIfNullOrWhiteSpace(repository);
        ArgumentException.ThrowIfNullOrWhiteSpace(branch);

        string path = $"repos/{Escape(owner)}/{Escape(repository)}"
            + $"/commits?sha={Escape(branch)}&per_page=1";
        GitHubReadResponse<GitHubCommitDto[]> response =
            await GetAsync<GitHubCommitDto[]>(path, cancellationToken);

        if (response.Value is null)
        {
            return GitHubFactResult<GitHubLatestCommit>.Failed(
                response.Failure ?? GitHubReadFailure.Unavailable);
        }

        GitHubCommitDto? commit = response.Value.FirstOrDefault();
        if (commit?.Sha is not { Length: 40 or 64 } sha || !sha.All(Uri.IsHexDigit))
        {
            // A branch with no commits, or a payload Console Ops cannot trust.
            return GitHubFactResult<GitHubLatestCommit>.Failed(GitHubReadFailure.InvalidResponse);
        }

        return GitHubFactResult<GitHubLatestCommit>.Success(new GitHubLatestCommit(
            sha,
            sha[..7],
            commit.Commit?.Committer?.Date ?? commit.Commit?.Author?.Date));
    }

    public async Task<GitHubFactResult<GitHubEndpointDetection>> DetectEndpointsAsync(
        string owner,
        string repository,
        string branch,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(owner);
        ArgumentException.ThrowIfNullOrWhiteSpace(repository);
        ArgumentException.ThrowIfNullOrWhiteSpace(branch);

        string treePath = $"repos/{Escape(owner)}/{Escape(repository)}"
            + $"/git/trees/{Escape(branch)}?recursive=1";
        GitHubReadResponse<GitHubTreeDto> tree =
            await GetAsync<GitHubTreeDto>(treePath, cancellationToken);

        if (tree.Value is null)
        {
            return GitHubFactResult<GitHubEndpointDetection>.Failed(
                tree.Failure ?? GitHubReadFailure.Unavailable);
        }

        string[] candidates = (tree.Value.Tree ?? [])
            .Where(entry => string.Equals(entry.Type, "blob", StringComparison.Ordinal))
            .Where(entry => IsCandidateFile(entry.Path))
            .Where(entry => entry.Size is null or <= MaximumFileBytes)
            .Select(entry => entry.Path!)
            .OrderBy(path => path.Count(character => character == '/'))
            .Take(MaximumInspectedFiles)
            .ToArray();

        List<GitHubDetectedEndpoint> detected = [];
        int inspected = 0;

        foreach (string path in candidates)
        {
            string? source = await ReadTextFileAsync(owner, repository, branch, path, cancellationToken);
            if (source is null)
            {
                continue;
            }

            inspected++;
            AddDetections(source, path, detected);
        }

        return GitHubFactResult<GitHubEndpointDetection>.Success(
            new GitHubEndpointDetection(DistinctByKind(detected), inspected));
    }

    private async Task<string?> ReadTextFileAsync(
        string owner,
        string repository,
        string branch,
        string path,
        CancellationToken cancellationToken)
    {
        string contentPath = $"repos/{Escape(owner)}/{Escape(repository)}"
            + $"/contents/{EscapePath(path)}?ref={Escape(branch)}";
        GitHubReadResponse<GitHubContentDto> response =
            await GetAsync<GitHubContentDto>(contentPath, cancellationToken);

        if (response.Value?.Content is not { Length: > 0 } encoded
            || !string.Equals(response.Value.Encoding, "base64", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        try
        {
            byte[] bytes = Convert.FromBase64String(encoded.Replace("\n", string.Empty));
            return bytes.Length > MaximumFileBytes ? null : Encoding.UTF8.GetString(bytes);
        }
        catch (FormatException)
        {
            return null;
        }
    }

    /// <summary>
    /// Recognises only literal paths registered on the application builder.
    /// </summary>
    /// <remarks>
    /// Requiring the <c>app.</c> receiver is what keeps this honest: a route registered on a
    /// <c>MapGroup</c> variable carries a prefix this cannot see, so it is skipped rather than reported
    /// at the wrong path. A path built from configuration is not a literal and is skipped too.
    /// </remarks>
    private static void AddDetections(string source, string file, List<GitHubDetectedEndpoint> into)
    {
        foreach (Match match in HealthCheckPattern.Matches(source))
        {
            into.Add(new GitHubDetectedEndpoint(
                GitHubDetectedEndpointKind.Health,
                match.Groups["path"].Value,
                file));
        }

        foreach (Match match in AppMapGetPattern.Matches(source))
        {
            string path = match.Groups["path"].Value;
            if (path.Contains("version", StringComparison.OrdinalIgnoreCase))
            {
                into.Add(new GitHubDetectedEndpoint(GitHubDetectedEndpointKind.Version, path, file));
            }
            else if (path.Contains("health", StringComparison.OrdinalIgnoreCase))
            {
                into.Add(new GitHubDetectedEndpoint(GitHubDetectedEndpointKind.Health, path, file));
            }
        }
    }

    /// <summary>One suggestion per kind: the shallowest file wins, and ties keep the first match.</summary>
    private static IReadOnlyList<GitHubDetectedEndpoint> DistinctByKind(
        IEnumerable<GitHubDetectedEndpoint> detected) =>
        detected
            .GroupBy(endpoint => endpoint.Kind)
            .Select(group => group.First())
            .OrderBy(endpoint => endpoint.Kind)
            .ToArray();

    private static bool IsCandidateFile(string? path)
    {
        if (path is null)
        {
            return false;
        }

        string name = FileNameOf(path);
        return string.Equals(name, "Program.cs", StringComparison.OrdinalIgnoreCase)
            || string.Equals(name, "Startup.cs", StringComparison.OrdinalIgnoreCase);
    }

    private static string EscapePath(string path) =>
        string.Join('/', path.Split('/').Select(Uri.EscapeDataString));

    public async Task<GitHubFactResult<GitHubBranchList>> ListBranchesAsync(
        string owner,
        string repository,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(owner);
        ArgumentException.ThrowIfNullOrWhiteSpace(repository);

        string path = $"repos/{Escape(owner)}/{Escape(repository)}/branches?per_page={PageSize}";
        GitHubReadResponse<GitHubBranchDto[]> response =
            await GetAsync<GitHubBranchDto[]>(path, cancellationToken);

        if (response.Value is null)
        {
            return GitHubFactResult<GitHubBranchList>.Failed(
                response.Failure ?? GitHubReadFailure.Unavailable);
        }

        string[] names = response.Value
            .Select(branch => branch.Name?.Trim())
            .Where(name => !string.IsNullOrEmpty(name))
            .Select(name => name!)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(name => name, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        return GitHubFactResult<GitHubBranchList>.Success(
            new GitHubBranchList(names, response.HasNextPage));
    }

    /// <summary>Every read goes through the shared GitHub request, so failure means the same thing here.</summary>
    private Task<GitHubReadResponse<T>> GetAsync<T>(
        string relativePath,
        CancellationToken cancellationToken)
        where T : class =>
        GitHubRead.GetAsync<T>(httpClient, relativePath, cancellationToken);
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

    private sealed record GitHubBranchDto(string? Name);

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

    private sealed record GitHubCommitDto(string? Sha, GitHubCommitDetailsDto? Commit);

    private sealed record GitHubCommitDetailsDto(
        GitHubCommitPersonDto? Author,
        GitHubCommitPersonDto? Committer);

    private sealed record GitHubCommitPersonDto(DateTimeOffset? Date);

    private sealed record GitHubTreeDto(GitHubTreeEntryDto[]? Tree);

    private sealed record GitHubTreeEntryDto(string? Path, string? Type, int? Size);

    private sealed record GitHubContentDto(string? Content, string? Encoding);
}
