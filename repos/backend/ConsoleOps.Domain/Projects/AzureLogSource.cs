using System.Text.RegularExpressions;

namespace ConsoleOps.Domain.Projects;

/// <summary>
/// Where an environment's application logs can be read from.
/// <para>
/// Console Ops reads logs the same way it reads everything else: by asking the provider. For an Azure
/// Container Apps environment that means a Log Analytics workspace plus the name of the container app
/// whose console output belongs to this environment.
/// </para>
/// <para>
/// Both parts are required together. Half a source cannot be queried, so it must not be storable: an
/// environment either has a log source or has none, and "none" is reported as not configured rather than
/// attempted and failed.
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

    private AzureLogSource(Guid workspaceId, string containerAppName)
    {
        WorkspaceId = workspaceId;
        ContainerAppName = containerAppName;
    }

    /// <summary>Log Analytics workspace id, as Azure reports it for the workspace.</summary>
    public Guid WorkspaceId { get; }

    /// <summary>Container app whose console logs belong to this environment.</summary>
    public string ContainerAppName { get; }

    /// <summary>
    /// Builds a log source, or returns <c>null</c> when neither part was supplied.
    /// </summary>
    /// <exception cref="ArgumentException">
    /// One part was supplied without the other, or a part is not valid.
    /// </exception>
    public static AzureLogSource? Create(Guid? workspaceId, string? containerAppName)
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
                "A log source needs a Log Analytics workspace id as well as a container app name.",
                nameof(workspaceId));
        }

        if (trimmedName is null)
        {
            throw new ArgumentException(
                "A log source needs a container app name as well as a workspace id.",
                nameof(containerAppName));
        }

        if (!IsValidContainerAppName(trimmedName))
        {
            throw new ArgumentException(
                "Container app name must be 2 to 32 characters of lower-case letters, digits, or single "
                + "hyphens, starting with a letter and ending with a letter or digit.",
                nameof(containerAppName));
        }

        return new AzureLogSource(presentWorkspaceId.Value, trimmedName);
    }

    /// <summary>
    /// Azure's own naming rule for a container app. Enforced here because the name reaches a provider
    /// query, so a value that cannot be a real app must never be stored, let alone interpolated.
    /// </summary>
    public static bool IsValidContainerAppName(string? value) =>
        value is not null
        && value.Length >= ContainerAppNameMinLength
        && value.Length <= ContainerAppNameMaxLength
        && ContainerAppNamePattern().IsMatch(value);

    [GeneratedRegex(@"^[a-z](?:[a-z0-9]|-(?!-))*[a-z0-9]$", RegexOptions.CultureInvariant)]
    private static partial Regex ContainerAppNamePattern();
}
