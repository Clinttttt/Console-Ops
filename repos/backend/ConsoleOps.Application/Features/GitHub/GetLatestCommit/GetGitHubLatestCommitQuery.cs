using ConsoleOps.Application.Abstractions.Messaging;
using ConsoleOps.Application.Integrations.GitHub;
using FluentValidation;
using MediatR;

namespace ConsoleOps.Application.Features.GitHub.GetLatestCommit;

/// <summary>
/// Reads the head commit of one branch, so a screen can compare source with a deployed commit before a
/// project is registered.
/// </summary>
public sealed record GetGitHubLatestCommitQuery(string Owner, string Repository, string Branch)
    : IRequest<Result<GitHubLatestCommitResponse>>;

public sealed record GitHubLatestCommitResponse(
    string CommitSha,
    string CommitShortSha,
    DateTimeOffset? CommittedAt);

public sealed class GetGitHubLatestCommitQueryValidator
    : AbstractValidator<GetGitHubLatestCommitQuery>
{
    /// <summary>GitHub owner and repository names, kept strict so nothing odd reaches the provider.</summary>
    private const string SegmentPattern = "^[A-Za-z0-9._-]+$";

    public GetGitHubLatestCommitQueryValidator()
    {
        RuleFor(query => query.Owner).NotEmpty().MaximumLength(100).Matches(SegmentPattern);
        RuleFor(query => query.Repository).NotEmpty().MaximumLength(100).Matches(SegmentPattern);

        // Branch names allow slashes, as in release/1.2.
        RuleFor(query => query.Branch)
            .NotEmpty()
            .MaximumLength(255)
            .Matches("^[A-Za-z0-9._/-]+$");
    }
}

public sealed class GetGitHubLatestCommitQueryHandler(IGitHubRepositoryCatalog catalog)
    : IRequestHandler<GetGitHubLatestCommitQuery, Result<GitHubLatestCommitResponse>>
{
    public async Task<Result<GitHubLatestCommitResponse>> Handle(
        GetGitHubLatestCommitQuery request,
        CancellationToken cancellationToken)
    {
        GitHubFactResult<GitHubLatestCommit> result = await catalog.GetLatestCommitAsync(
            request.Owner,
            request.Repository,
            request.Branch,
            cancellationToken);

        if (result.Observation is null)
        {
            return Result<GitHubLatestCommitResponse>.Failure(
                GitHubDiscoveryErrors.From(result.Failure ?? GitHubReadFailure.Unavailable));
        }

        return Result<GitHubLatestCommitResponse>.Success(new GitHubLatestCommitResponse(
            result.Observation.CommitSha,
            result.Observation.ShortCommitSha,
            result.Observation.CommittedAtUtc));
    }
}
