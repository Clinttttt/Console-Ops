namespace ConsoleOps.Application.Integrations.GitHub;

public interface IGitHubProjectReader
{
    Task<GitHubProjectReadResult> ReadAsync(
        GitHubProjectReference project,
        CancellationToken cancellationToken);
}
