using ConsoleOps.Application.Abstractions.Messaging;
using ConsoleOps.Application.Integrations.GitHub;
using FluentValidation;
using MediatR;

namespace ConsoleOps.Application.Features.GitHub.ListWorkflows;

public sealed record ListGitHubWorkflowsQuery(string Owner, string Repository)
    : IRequest<Result<GitHubWorkflowsResponse>>;

public sealed record GitHubWorkflowsResponse(IReadOnlyList<GitHubWorkflowResponse> Workflows);

/// <param name="LatestRunConclusion">Camel-case conclusion such as <c>success</c> or <c>never</c>.</param>
public sealed record GitHubWorkflowResponse(
    string Name,
    string Path,
    string FileName,
    bool Active,
    string LatestRunConclusion,
    DateTimeOffset? LatestRunCompletedAt);

public sealed class ListGitHubWorkflowsQueryValidator : AbstractValidator<ListGitHubWorkflowsQuery>
{
    /// <summary>GitHub owner and repository names, kept strict so nothing odd reaches the provider.</summary>
    private const string SegmentPattern = "^[A-Za-z0-9._-]+$";

    public ListGitHubWorkflowsQueryValidator()
    {
        RuleFor(query => query.Owner)
            .NotEmpty()
            .MaximumLength(100)
            .Matches(SegmentPattern);

        RuleFor(query => query.Repository)
            .NotEmpty()
            .MaximumLength(100)
            .Matches(SegmentPattern);
    }
}

public sealed class ListGitHubWorkflowsQueryHandler(IGitHubRepositoryCatalog catalog)
    : IRequestHandler<ListGitHubWorkflowsQuery, Result<GitHubWorkflowsResponse>>
{
    public async Task<Result<GitHubWorkflowsResponse>> Handle(
        ListGitHubWorkflowsQuery request,
        CancellationToken cancellationToken)
    {
        GitHubFactResult<GitHubWorkflowCatalog> result = await catalog.ListWorkflowsAsync(
            request.Owner,
            request.Repository,
            cancellationToken);

        if (result.Observation is null)
        {
            return Result<GitHubWorkflowsResponse>.Failure(
                GitHubDiscoveryErrors.From(result.Failure ?? GitHubReadFailure.Unavailable));
        }

        GitHubWorkflowResponse[] workflows = result.Observation.Workflows
            .Select(workflow => new GitHubWorkflowResponse(
                workflow.Name,
                workflow.Path,
                workflow.FileName,
                workflow.Active,
                ToCamelCase(workflow.LatestRunConclusion),
                workflow.LatestRunCompletedAtUtc))
            .ToArray();

        return Result<GitHubWorkflowsResponse>.Success(new GitHubWorkflowsResponse(workflows));
    }

    /// <summary>Enums cross the wire as camel-case strings, as the dashboard response does.</summary>
    private static string ToCamelCase(GitHubWorkflowRunConclusion value)
    {
        string name = value.ToString();
        return string.Concat(char.ToLowerInvariant(name[0]), name[1..]);
    }
}
