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
    string? VersionUrl);

public sealed record UpdateProjectEnvironmentRequest(
    Guid? Id,
    string Name,
    string Kind,
    string? ApplicationUrl,
    string? HealthUrl,
    string? VersionUrl);
