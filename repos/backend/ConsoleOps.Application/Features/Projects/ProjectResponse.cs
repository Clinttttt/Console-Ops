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
    string ContainerAppName,
    AzureLogPlatform Platform = AzureLogPlatform.ContainerApp)
{
    /// <summary>
    /// Maps a stored source, naming the provider from its platform.
    /// </summary>
    /// <remarks>
    /// One definition, used by both the command responses and the read store, because a provider name derived in
    /// two places is a provider name that will eventually be derived differently.
    /// </remarks>
    public static ProjectLogSourceResponse From(AzureLogSource source)
    {
        ArgumentNullException.ThrowIfNull(source);

        return new ProjectLogSourceResponse(
            ProviderName(source.Platform),
            source.WorkspaceId,
            source.ContainerAppName,
            source.Platform);
    }

    public static string ProviderName(AzureLogPlatform platform) => platform switch
    {
        AzureLogPlatform.AppService => "azureAppService",
        _ => "azureContainerApps"
    };
}

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
    /// <remarks>
    /// The name is derived from the platform rather than stored beside it, so the two can never disagree about
    /// which service a source belongs to.
    /// </remarks>
    internal static ProjectLogSourceResponse? ToLogSource(AzureLogSource? source) =>
        source is null ? null : ProjectLogSourceResponse.From(source);
}
