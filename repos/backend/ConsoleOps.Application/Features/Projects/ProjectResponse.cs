using ConsoleOps.Domain.Projects;

namespace ConsoleOps.Application.Features.Projects;

public sealed record ProjectResponse(
    Guid Id,
    string Name,
    string? Description,
    ProjectRepositoryResponse Repository,
    IReadOnlyCollection<ProjectEnvironmentResponse> Environments,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset? UpdatedAtUtc,
    long ConfigurationVersion);

public sealed record ProjectRepositoryResponse(
    string Owner,
    string Name,
    string DefaultBranch,
    string? WorkflowFile);

public sealed record ProjectEnvironmentResponse(
    Guid Id,
    string Name,
    string Kind,
    string? ApplicationUrl,
    string? HealthUrl,
    string? VersionUrl);

internal static class ProjectResponseMapper
{
    public static ProjectResponse FromDomain(Project project) => new(
        project.Id,
        project.Name,
        project.Description,
        new ProjectRepositoryResponse(
            project.RepositoryOwner,
            project.RepositoryName,
            project.DefaultBranch,
            project.WorkflowFile),
        project.Environments
            .OrderBy(environment => environment.Kind)
            .ThenBy(environment => environment.Name)
            .Select(environment => new ProjectEnvironmentResponse(
                environment.Id,
                environment.Name,
                environment.Kind.ToString().ToLowerInvariant(),
                environment.ApplicationUrl,
                environment.HealthUrl,
                environment.VersionUrl))
            .ToArray(),
        project.CreatedAtUtc,
        project.UpdatedAtUtc,
        project.ConfigurationVersion);
}
