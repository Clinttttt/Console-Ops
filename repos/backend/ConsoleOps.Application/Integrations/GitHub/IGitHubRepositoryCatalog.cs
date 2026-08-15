namespace ConsoleOps.Application.Integrations.GitHub;

/// <summary>
/// Reads what GitHub already knows so registration does not ask the operator to retype it.
/// </summary>
/// <remarks>
/// Discovery only. Nothing here selects a repository or a workflow on the operator's behalf, and
/// nothing is persisted: these are provider reads for the Add Project import flow.
/// </remarks>
public interface IGitHubRepositoryCatalog
{
    /// <param name="query">Optional case-insensitive filter on owner and repository name.</param>
    Task<GitHubFactResult<GitHubRepositoryCatalogPage>> ListRepositoriesAsync(
        string? query,
        CancellationToken cancellationToken);

    Task<GitHubFactResult<GitHubWorkflowCatalog>> ListWorkflowsAsync(
        string owner,
        string repository,
        CancellationToken cancellationToken);

    /// <summary>
    /// Reads the head commit of one branch so a screen can compare source with a deployed commit.
    /// </summary>
    Task<GitHubFactResult<GitHubLatestCommit>> GetLatestCommitAsync(
        string owner,
        string repository,
        string branch,
        CancellationToken cancellationToken);

    /// <summary>
    /// Looks for health and version endpoint paths in a bounded set of repository source files.
    /// </summary>
    /// <remarks>
    /// Best effort and deliberately narrow. It reports only paths written as string literals on the
    /// application builder, so a route composed from a group prefix or read from configuration yields
    /// nothing rather than a guess.
    /// </remarks>
    Task<GitHubFactResult<GitHubEndpointDetection>> DetectEndpointsAsync(
        string owner,
        string repository,
        string branch,
        CancellationToken cancellationToken);
}
