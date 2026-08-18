namespace ConsoleOps.Application.Integrations.AzureMonitor;

/// <summary>
/// Lists the Azure resources Console Ops could read logs from.
/// <para>
/// Discovery only. It reads Azure's resource inventory so an operator can pick a log source instead of
/// typing a workspace GUID by hand, and it never changes anything in Azure.
/// </para>
/// <para>
/// It lists every service type it knows how to name, not only the ones Console Ops can read yet. An
/// operator who cannot find their App Service here has no way to tell "Azure does not have it" from
/// "Console Ops does not look for it", and that question came up twice before this existed.
/// </para>
/// </summary>
public interface IAzureLogSourceCatalog
{
    Task<AzureLogSourceCatalogResult> ListLogSourcesAsync(
        string? query,
        CancellationToken cancellationToken);
}

/// <param name="HasMore">
/// <c>true</c> when Azure had more resources than the bounded page returned, so the UI can say the list
/// is not everything rather than implying it is.
/// </param>
public sealed record AzureLogSourceCatalogResult(
    IReadOnlyList<AzureLogSourceCandidate> Sources,
    bool HasMore,
    AzureCatalogFailure? Failure)
{
    public bool IsSuccess => Failure is null;

    public static AzureLogSourceCatalogResult Success(
        IReadOnlyList<AzureLogSourceCandidate> sources,
        bool hasMore) =>
        new(sources, hasMore, null);

    public static AzureLogSourceCatalogResult Failed(AzureCatalogFailure failure) =>
        new([], false, failure);
}

/// <summary>
/// One Azure resource that hosts an application, with whatever Console Ops needs to read its logs.
/// </summary>
/// <param name="Platform">Which Azure service hosts it, because logs are not read the same way for each.</param>
/// <param name="WorkspaceId">
/// The Log Analytics workspace its logs are sent to, or <c>null</c> when Console Ops could not establish one.
/// A null workspace means these logs cannot be read yet, which is worth showing rather than hiding.
/// </param>
/// <param name="EnvironmentName">
/// The Container Apps environment that owns the app, or <c>null</c> for platforms that have no equivalent.
/// </param>
/// <param name="ApplicationUrl">
/// The resource's own public address, or <c>null</c> when it has none Console Ops could reach.
/// <para>
/// Read from Azure rather than composed from a naming convention: an App Service host name is generated and
/// unguessable, and a container app's FQDN only resolves outside its environment when ingress is external.
/// It exists so registering a project does not require an operator to copy a host name by hand.
/// </para>
/// </param>
public sealed record AzureLogSourceCandidate(
    string Name,
    AzureLogPlatform Platform,
    string ResourceGroup,
    string SubscriptionId,
    string? Location,
    string? EnvironmentName,
    Guid? WorkspaceId,
    string? ApplicationUrl);

/// <summary>
/// The Azure services Console Ops can name. Naming one is not the same as being able to read it: a reader
/// exists per platform, and <see cref="AzureLogPlatformSupport"/> is the single place that says which.
/// </summary>
public enum AzureLogPlatform
{
    ContainerApp,
    AppService
}

/// <summary>
/// Which discovered platforms Console Ops can actually read logs from.
/// <para>
/// Kept as one predicate rather than scattered checks so that adding a reader is a single edit, and so the
/// screen can never offer a source that nothing can read.
/// </para>
/// </summary>
public static class AzureLogPlatformSupport
{
    public static bool CanRead(AzureLogPlatform platform) => platform switch
    {
        AzureLogPlatform.ContainerApp => true,
        // App Service console output lands in different tables, and only when a diagnostic setting sends it
        // to a workspace. No reader is written for it until there are real rows to verify one against.
        _ => false
    };
}

/// <summary>
/// Why a discovery read produced nothing. Same vocabulary as the other providers, so the API can tell
/// "nothing to list" apart from "could not ask".
/// </summary>
public enum AzureCatalogFailure
{
    Unauthorized,
    NotFound,
    RateLimited,
    Unavailable,
    InvalidResponse
}
