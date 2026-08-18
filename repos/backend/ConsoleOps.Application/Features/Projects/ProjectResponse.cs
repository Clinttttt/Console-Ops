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
    string? VersionUrl,
    ProjectLogSourceResponse? LogSource);

/// <summary>
/// Where this environment's logs are read from. `null` when no source is configured, which the screens
/// report as not configured rather than as an empty stream.
/// </summary>
public sealed record ProjectLogSourceResponse(
    string Provider,
    Guid WorkspaceId,
    string ContainerAppName);

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
                environment.VersionUrl,
                ToLogSource(environment.LogSource)))
            .ToArray(),
        project.CreatedAtUtc,
        project.UpdatedAtUtc,
        project.ConfigurationVersion);

    /// <summary>The provider is named so a second log source cannot be mistaken for this one.</summary>
    internal static ProjectLogSourceResponse? ToLogSource(AzureLogSource? source) =>
        source is null
            ? null
            : new ProjectLogSourceResponse("azureContainerApps", source.WorkspaceId, source.ContainerAppName);
}
