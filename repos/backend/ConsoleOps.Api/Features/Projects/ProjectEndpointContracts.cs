namespace ConsoleOps.Api.Features.Projects;

public sealed record ProjectRepositoryRequest(
    string Owner,
    string Name,
    string DefaultBranch,
    string? WorkflowFile);

public sealed record RegisterProjectEnvironmentRequest(
    string Name,
    string Kind,
    string? ApplicationUrl,
    string? HealthUrl,
    string? VersionUrl,
    ProjectLogSourceRequest? LogSource = null);

public sealed record UpdateProjectEnvironmentRequest(
    Guid? Id,
    string Name,
    string Kind,
    string? ApplicationUrl,
    string? HealthUrl,
    string? VersionUrl,
    ProjectLogSourceRequest? LogSource = null);

/// <summary>
/// Where this environment's application logs can be read from. Optional, and both parts are required
/// together. No credential: Console Ops authenticates to Azure from its own configuration.
/// </summary>
public sealed record ProjectLogSourceRequest(Guid? WorkspaceId, string? ContainerAppName);
