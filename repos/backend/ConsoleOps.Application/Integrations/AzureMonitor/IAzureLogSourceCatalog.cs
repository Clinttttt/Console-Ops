namespace ConsoleOps.Application.Integrations.AzureMonitor;

/// <summary>
/// Lists the container apps Console Ops could read logs from.
/// <para>
/// Discovery only. It reads Azure's resource inventory so an operator can pick a log source instead of
/// typing a workspace GUID by hand, and it never changes anything in Azure.
/// </para>
/// </summary>
public interface IAzureLogSourceCatalog
{
    Task<AzureLogSourceCatalogResult> ListContainerAppsAsync(
        string? query,
        CancellationToken cancellationToken);
}

/// <param name="HasMore">
/// <c>true</c> when Azure had more resources than the bounded page returned, so the UI can say the list
/// is not everything rather than implying it is.
/// </param>
public sealed record AzureLogSourceCatalogResult(
    IReadOnlyList<AzureContainerAppLogSource> ContainerApps,
    bool HasMore,
    AzureCatalogFailure? Failure)
{
    public bool IsSuccess => Failure is null;

    public static AzureLogSourceCatalogResult Success(
        IReadOnlyList<AzureContainerAppLogSource> containerApps,
        bool hasMore) =>
        new(containerApps, hasMore, null);

    public static AzureLogSourceCatalogResult Failed(AzureCatalogFailure failure) =>
        new([], false, failure);
}

/// <summary>
/// One container app, with the workspace its environment sends console logs to.
/// </summary>
/// <param name="WorkspaceId">
/// The Log Analytics workspace of the app's Container Apps environment, or <c>null</c> when that
/// environment has no log configuration. A null workspace means Console Ops cannot read this app's logs
/// yet, which is worth showing rather than hiding.
/// </param>
public sealed record AzureContainerAppLogSource(
    string Name,
    string ResourceGroup,
    string SubscriptionId,
    string? Location,
    string? EnvironmentName,
    Guid? WorkspaceId);

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
