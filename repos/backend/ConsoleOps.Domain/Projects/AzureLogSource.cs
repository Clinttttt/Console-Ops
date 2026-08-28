using System.Text.RegularExpressions;

namespace ConsoleOps.Domain.Projects;

/// <summary>
/// Where an environment's application logs can be read from.
/// <para>
/// Console Ops reads logs the same way it reads everything else: by asking the provider. That means a Log
/// Analytics workspace, the name of the Azure resource whose console output belongs to this environment, and
/// which Azure service that resource is - because the log tables and the naming rules both differ by service.
/// </para>
/// <para>
/// All parts are required together. Half a source cannot be queried, so it must not be storable: an
/// environment either has a log source or has none, and "none" is reported as not configured rather than
/// attempted and failed.
/// </para>
/// <para>
/// The name property is still called <c>ContainerAppName</c>, which reads oddly for an App Service site. It is
/// deliberate: renaming it reaches twenty-three files, a stored column and the wire contract, and none of that
/// is needed to read a second platform. The rename belongs in its own change rather than hidden inside this one.
/// </para>
/// <para>
/// No credential lives here. Console Ops authenticates to Azure from its own configuration, so a project
/// row never carries a secret.
/// </para>
/// </summary>
public sealed partial record AzureLogSource
{
    public const int ContainerAppNameMaxLength = 32;
    private const int ContainerAppNameMinLength = 2;

    /// <summary>Azure's limit for a site name, which is longer and less restrictive than a container app's.</summary>
    public const int SiteNameMaxLength = 60;
    private const int SiteNameMinLength = 2;

    private AzureLogSource(Guid workspaceId, string containerAppName, AzureLogPlatform platform)
    {
        WorkspaceId = workspaceId;
        ContainerAppName = containerAppName;
        Platform = platform;
    }

    /// <summary>Log Analytics workspace id, as Azure reports it for the workspace.</summary>
    public Guid WorkspaceId { get; }

    /// <summary>The Azure resource whose console logs belong to this environment.</summary>
    public string ContainerAppName { get; }

    /// <summary>Which Azure service hosts that resource, which decides how its logs are read.</summary>
    public AzureLogPlatform Platform { get; }

    /// <summary>
    /// Builds a log source, or returns <c>null</c> when neither part was supplied.
    /// </summary>
    /// <exception cref="ArgumentException">
    /// One part was supplied without the other, or a part is not valid for the platform.
    /// </exception>
    public static AzureLogSource? Create(
        Guid? workspaceId,
        string? containerAppName,
        AzureLogPlatform platform = AzureLogPlatform.ContainerApp)
    {
        string? trimmedName = string.IsNullOrWhiteSpace(containerAppName) ? null : containerAppName.Trim();
        Guid? presentWorkspaceId = workspaceId is null || workspaceId == Guid.Empty ? null : workspaceId;

        if (presentWorkspaceId is null && trimmedName is null)
        {
            return null;
        }

        if (presentWorkspaceId is null)
        {
            throw new ArgumentException(
                "A log source needs a Log Analytics workspace id as well as a resource name.",
                nameof(workspaceId));
        }

        if (trimmedName is null)
        {
            throw new ArgumentException(
                "A log source needs a resource name as well as a workspace id.",
                nameof(containerAppName));
        }

        if (!IsValidResourceName(trimmedName, platform))
        {
            throw new ArgumentException(DescribeNameRule(platform), nameof(containerAppName));
        }

        return new AzureLogSource(presentWorkspaceId.Value, trimmedName, platform);
    }

    /// <summary>
    /// Azure's own naming rule for the platform. Enforced here because the name reaches a provider query, so
    /// a value that cannot be a real resource must never be stored, let alone interpolated.
    /// </summary>
    public static bool IsValidResourceName(string? value, AzureLogPlatform platform) => platform switch
    {
        AzureLogPlatform.ContainerApp => IsValidContainerAppName(value),
        AzureLogPlatform.AppService => IsValidSiteName(value),
        _ => false
    };

    public static bool IsValidContainerAppName(string? value) =>
        value is not null
        && value.Length >= ContainerAppNameMinLength
        && value.Length <= ContainerAppNameMaxLength
        && ContainerAppNamePattern().IsMatch(value);

    /// <summary>
    /// Azure's rule for an App Service site name: letters, digits and hyphens, not starting or ending with a
    /// hyphen. Unlike a container app it may be mixed case and may run to sixty characters, which is why the
    /// container app rule cannot simply be reused - it would refuse real sites.
    /// </summary>
    public static bool IsValidSiteName(string? value) =>
        value is not null
        && value.Length >= SiteNameMinLength
        && value.Length <= SiteNameMaxLength
        && SiteNamePattern().IsMatch(value);

    private static string DescribeNameRule(AzureLogPlatform platform) => platform switch
    {
        AzureLogPlatform.AppService =>
            "App Service name must be 2 to 60 characters of letters, digits, or hyphens, and may not start "
            + "or end with a hyphen.",
        _ =>
            "Container app name must be 2 to 32 characters of lower-case letters, digits, or single "
            + "hyphens, starting with a letter and ending with a letter or digit."
    };

    [GeneratedRegex(@"^[a-z](?:[a-z0-9]|-(?!-))*[a-z0-9]$", RegexOptions.CultureInvariant)]
    private static partial Regex ContainerAppNamePattern();

    [GeneratedRegex(@"^[A-Za-z0-9](?:[A-Za-z0-9-]*[A-Za-z0-9])?$", RegexOptions.CultureInvariant)]
    private static partial Regex SiteNamePattern();
}
