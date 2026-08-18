namespace ConsoleOps.Application.Features.Projects;

/// <summary>
/// Where an environment's application logs are read from, as supplied by the operator.
/// <para>
/// Optional, and both parts are required together: Console Ops queries the provider, so half a source
/// could only fail. Shared by registration and update because both edit the same configuration.
/// </para>
/// <para>
/// Carries no credential. Console Ops authenticates to Azure from its own configuration, so a project
/// never stores a secret.
/// </para>
/// </summary>
public sealed record ProjectLogSource(Guid? WorkspaceId, string? ContainerAppName);
