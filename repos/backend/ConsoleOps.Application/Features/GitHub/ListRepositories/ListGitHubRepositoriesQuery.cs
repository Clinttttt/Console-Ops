using ConsoleOps.Application.Abstractions.Messaging;
using ConsoleOps.Application.Integrations.GitHub;
using MediatR;

namespace ConsoleOps.Application.Features.GitHub.ListRepositories;

/// <param name="Query">Optional case-insensitive filter on owner and repository name.</param>
public sealed record ListGitHubRepositoriesQuery(string? Query)
    : IRequest<Result<GitHubRepositoriesResponse>>;

public sealed record GitHubRepositoriesResponse(
    IReadOnlyList<GitHubRepositoryResponse> Repositories,
    bool HasMore);

public sealed record GitHubRepositoryResponse(
    string Owner,
    string Name,
    string DefaultBranch,
    bool IsPrivate,
    string? Language,
    DateTimeOffset? PushedAt,
    string? HtmlUrl);

public sealed class ListGitHubRepositoriesQueryHandler(IGitHubRepositoryCatalog catalog)
    : IRequestHandler<ListGitHubRepositoriesQuery, Result<GitHubRepositoriesResponse>>
{
    public async Task<Result<GitHubRepositoriesResponse>> Handle(
        ListGitHubRepositoriesQuery request,
        CancellationToken cancellationToken)
    {
        GitHubFactResult<GitHubRepositoryCatalogPage> result =
            await catalog.ListRepositoriesAsync(request.Query, cancellationToken);

        if (result.Observation is null)
        {
            return Result<GitHubRepositoriesResponse>.Failure(
                GitHubDiscoveryErrors.From(result.Failure ?? GitHubReadFailure.Unavailable));
        }

        GitHubRepositoryResponse[] repositories = result.Observation.Repositories
            .Select(repository => new GitHubRepositoryResponse(
                repository.Owner,
                repository.Name,
                repository.DefaultBranch,
                repository.IsPrivate,
                repository.Language,
                repository.PushedAtUtc,
                repository.HtmlUrl))
            .ToArray();

        return Result<GitHubRepositoriesResponse>.Success(
            new GitHubRepositoriesResponse(repositories, result.Observation.HasMore));
    }
}
