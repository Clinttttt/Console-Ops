using ConsoleOps.Application.Abstractions.Messaging;
using ConsoleOps.Application.Integrations.GitHub;
using FluentValidation;
using MediatR;

namespace ConsoleOps.Application.Features.GitHub.DetectEndpoints;

/// <summary>
/// Suggests health and version endpoint paths found in repository source.
/// </summary>
/// <remarks>
/// Suggestions only. The response says these were detected, never that they are configured or
/// reachable; the operator confirms each one, and verification proves it.
/// </remarks>
public sealed record DetectGitHubEndpointsQuery(string Owner, string Repository, string Branch)
    : IRequest<Result<GitHubDetectedEndpointsResponse>>;

public sealed record GitHubDetectedEndpointsResponse(
    IReadOnlyList<GitHubDetectedEndpointResponse> Endpoints,
    int InspectedFileCount);

/// <param name="Kind">Camel-case kind: <c>health</c> or <c>version</c>.</param>
/// <param name="SourceFile">Repository path the path was read from, so the operator can check it.</param>
public sealed record GitHubDetectedEndpointResponse(string Kind, string Path, string SourceFile);

public sealed class DetectGitHubEndpointsQueryValidator
    : AbstractValidator<DetectGitHubEndpointsQuery>
{
    private const string SegmentPattern = "^[A-Za-z0-9._-]+$";

    public DetectGitHubEndpointsQueryValidator()
    {
        RuleFor(query => query.Owner).NotEmpty().MaximumLength(100).Matches(SegmentPattern);
        RuleFor(query => query.Repository).NotEmpty().MaximumLength(100).Matches(SegmentPattern);
        RuleFor(query => query.Branch)
            .NotEmpty()
            .MaximumLength(255)
            .Matches("^[A-Za-z0-9._/-]+$");
    }
}

public sealed class DetectGitHubEndpointsQueryHandler(IGitHubRepositoryCatalog catalog)
    : IRequestHandler<DetectGitHubEndpointsQuery, Result<GitHubDetectedEndpointsResponse>>
{
    public async Task<Result<GitHubDetectedEndpointsResponse>> Handle(
        DetectGitHubEndpointsQuery request,
        CancellationToken cancellationToken)
    {
        GitHubFactResult<GitHubEndpointDetection> result = await catalog.DetectEndpointsAsync(
            request.Owner,
            request.Repository,
            request.Branch,
            cancellationToken);

        if (result.Observation is null)
        {
            return Result<GitHubDetectedEndpointsResponse>.Failure(
                GitHubDiscoveryErrors.From(result.Failure ?? GitHubReadFailure.Unavailable));
        }

        GitHubDetectedEndpointResponse[] endpoints = result.Observation.Endpoints
            .Select(endpoint => new GitHubDetectedEndpointResponse(
                ToCamelCase(endpoint.Kind),
                endpoint.Path,
                endpoint.SourceFile))
            .ToArray();

        return Result<GitHubDetectedEndpointsResponse>.Success(
            new GitHubDetectedEndpointsResponse(endpoints, result.Observation.InspectedFileCount));
    }

    private static string ToCamelCase(GitHubDetectedEndpointKind kind)
    {
        string name = kind.ToString();
        return string.Concat(char.ToLowerInvariant(name[0]), name[1..]);
    }
}
