using ConsoleOps.Domain.Projects;

namespace ConsoleOps.Application.Features.Projects;

/// <summary>
/// Where an environment's application logs are read from, as supplied by the operator.
/// <para>
/// Optional, and the workspace and name are required together: Console Ops queries the provider, so half a
/// source could only fail. Shared by registration and update because both edit the same configuration.
/// </para>
/// <para>
/// The platform decides which log table is read and which naming rule applies, so it travels with the source.
/// It defaults to a container app: that is what every source stored before this existed, and defaulting keeps
/// an older caller from having its meaning changed underneath it.
/// </para>
/// <para>
/// Carries no credential. Console Ops authenticates to Azure from its own configuration, so a project
/// never stores a secret.
/// </para>
/// </summary>
public sealed record ProjectLogSource(
    Guid? WorkspaceId,
    string? ContainerAppName,
    AzureLogPlatform Platform = AzureLogPlatform.ContainerApp);
