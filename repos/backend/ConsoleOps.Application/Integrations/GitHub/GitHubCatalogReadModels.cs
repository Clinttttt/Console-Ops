namespace ConsoleOps.Application.Integrations.GitHub;

/// <summary>
/// Repository the configured GitHub credential can see. A provider read, never a stored entity.
/// </summary>
public sealed record GitHubRepositorySummary(
    string Owner,
    string Name,
    string DefaultBranch,
    bool IsPrivate,
    string? Language,
    DateTimeOffset? PushedAtUtc,
    string? HtmlUrl);

public sealed record GitHubRepositoryCatalogPage(
    IReadOnlyList<GitHubRepositorySummary> Repositories,
    bool HasMore);

/// <summary>Outcome of a workflow's most recent run, as GitHub reported it.</summary>
public enum GitHubWorkflowRunConclusion
{
    Success,
    Failure,
    Cancelled,
    InProgress,
    Unknown,

    /// <summary>The workflow exists but GitHub reports no runs for it.</summary>
    Never
}

public sealed record GitHubWorkflowSummary(
    string Name,
    string Path,
    string FileName,
    bool Active,
    GitHubWorkflowRunConclusion LatestRunConclusion,
    DateTimeOffset? LatestRunCompletedAtUtc);

public sealed record GitHubWorkflowCatalog(IReadOnlyList<GitHubWorkflowSummary> Workflows);

/// <summary>
/// Head commit of one branch. Read for a single repository the operator has chosen, never for a list.
/// </summary>
public sealed record GitHubLatestCommit(
    string CommitSha,
    string ShortCommitSha,
    DateTimeOffset? CommittedAtUtc);
