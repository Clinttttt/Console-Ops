namespace ConsoleOps.Domain.Projects;

/// <summary>
/// Which Azure service hosts an application whose logs Console Ops reads.
/// </summary>
/// <remarks>
/// In the domain because it changes what a valid log source is, not only how one is queried: a container app
/// name and an App Service site name follow different rules, and the domain is what refuses a name that could
/// never be real. Whether a reader exists for a platform is a separate question, answered outside the domain.
/// </remarks>
public enum AzureLogPlatform
{
    ContainerApp,
    AppService
}
