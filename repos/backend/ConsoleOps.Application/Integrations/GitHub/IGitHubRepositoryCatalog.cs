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
}
