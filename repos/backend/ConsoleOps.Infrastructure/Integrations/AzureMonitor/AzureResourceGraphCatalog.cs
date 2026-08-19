using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Azure.Core;
using ConsoleOps.Application.Integrations.AzureMonitor;

namespace ConsoleOps.Infrastructure.Integrations.AzureMonitor;

/// <summary>
/// Lists container apps through Azure Resource Graph.
/// <para>
/// One POST to the resource inventory, read-only, bounded to a page. Provider shapes stay inside this
/// adapter and the port's models are what leaves it, exactly as the GitHub catalog works.
/// </para>
/// <para>
/// The credential is Console Ops' own and needs only read access. A rejected or missing credential is
/// reported as a failure the UI can explain, never as an empty list, because "you have no container apps"
/// and "Console Ops could not ask" are different facts.
/// </para>
/// </summary>
internal sealed class AzureResourceGraphCatalog(
    HttpClient httpClient,
    TokenCredential credential) : IAzureLogSourceCatalog
{
    internal const string ApiVersion = "2024-04-01";
    internal const string ManagementScope = "https://management.azure.com/.default";

    /// <summary>Bounded page: a picker is for choosing, not for browsing an entire tenant.</summary>
    internal const int PageSize = 200;

    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    public async Task<AzureLogSourceCatalogResult> ListLogSourcesAsync(
        string? query,
        CancellationToken cancellationToken)
    {
        string kql = AzureLogSourceDiscoveryQuery.Build(query, PageSize);

        try
        {
            AccessToken token = await credential.GetTokenAsync(
                new TokenRequestContext([ManagementScope]),
                cancellationToken);

            using HttpRequestMessage request = new(
                HttpMethod.Post,
                $"providers/Microsoft.ResourceGraph/resources?api-version={ApiVersion}")
            {
                Content = JsonContent.Create(new ResourceGraphRequest(
                    kql,
                    new ResourceGraphRequestOptions(PageSize, true))),
            };
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token.Token);

            using HttpResponseMessage response = await httpClient.SendAsync(
                request,
                HttpCompletionOption.ResponseHeadersRead,
                cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                return AzureLogSourceCatalogResult.Failed(MapFailure(response.StatusCode));
            }

            await using Stream stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            ResourceGraphResponse? payload = await JsonSerializer.DeserializeAsync<ResourceGraphResponse>(
                stream,
                SerializerOptions,
                cancellationToken);
            if (payload?.Data is null)
            {
                return AzureLogSourceCatalogResult.Failed(AzureCatalogFailure.InvalidResponse);
            }

            AzureLogSourceCandidate[] sources = payload.Data
                .Where(row => !string.IsNullOrWhiteSpace(row.Name))
                .Select(row => new AzureLogSourceCandidate(
                    row.Name!.Trim(),
                    ParsePlatform(row.Platform),
                    row.ResourceGroup?.Trim() ?? string.Empty,
                    row.SubscriptionId?.Trim() ?? string.Empty,
                    NullIfWhiteSpace(row.Location),
                    NullIfWhiteSpace(row.EnvironmentName),
                    ParseWorkspaceId(row.WorkspaceId),
                    ComposeApplicationUrl(row)))
                .ToArray();
            bool hasMore = payload.TotalRecords > sources.Length
                || string.Equals(payload.ResultTruncated, "true", StringComparison.OrdinalIgnoreCase);

            return AzureLogSourceCatalogResult.Success(sources, hasMore);
        }
        catch (Azure.RequestFailedException failure)
        {
            // Raised while acquiring a token: no signed-in identity, or one Azure rejected.
            return AzureLogSourceCatalogResult.Failed(
                failure.Status is 401 or 403
                    ? AzureCatalogFailure.Unauthorized
                    : AzureCatalogFailure.Unavailable);
        }
        catch (Azure.Identity.CredentialUnavailableException)
        {
            return AzureLogSourceCatalogResult.Failed(AzureCatalogFailure.Unauthorized);
        }
        catch (Azure.Identity.AuthenticationFailedException)
        {
            return AzureLogSourceCatalogResult.Failed(AzureCatalogFailure.Unauthorized);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return AzureLogSourceCatalogResult.Failed(AzureCatalogFailure.Unavailable);
        }
        catch (HttpRequestException)
        {
            return AzureLogSourceCatalogResult.Failed(AzureCatalogFailure.Unavailable);
        }
        catch (JsonException)
        {
            return AzureLogSourceCatalogResult.Failed(AzureCatalogFailure.InvalidResponse);
        }
    }

    private static AzureCatalogFailure MapFailure(HttpStatusCode status) => status switch
    {
        HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden => AzureCatalogFailure.Unauthorized,
        HttpStatusCode.NotFound => AzureCatalogFailure.NotFound,
        HttpStatusCode.TooManyRequests => AzureCatalogFailure.RateLimited,
        HttpStatusCode.BadRequest => AzureCatalogFailure.InvalidResponse,
        >= HttpStatusCode.InternalServerError => AzureCatalogFailure.Unavailable,
        _ => AzureCatalogFailure.Unavailable
    };

    /// <summary>An environment with no log configuration reports no workspace, which is not an error.</summary>
    private static Guid? ParseWorkspaceId(string? value) =>
        Guid.TryParse(value, out Guid workspaceId) && workspaceId != Guid.Empty ? workspaceId : null;

    /// <summary>
    /// The platform the query labelled the row with. An unrecognized label is treated as a container app
    /// because that is the only type this query asks for besides a site, and guessing wrong would only
    /// mislabel a row rather than lose it.
    /// </summary>
    private static AzureLogPlatform ParsePlatform(string? value) =>
        string.Equals(value?.Trim(), "appService", StringComparison.OrdinalIgnoreCase)
            ? AzureLogPlatform.AppService
            : AzureLogPlatform.ContainerApp;

    /// <summary>
    /// The resource''s public address, or <c>null</c> when it has none Console Ops could reach.
    /// <para>
    /// A container app FQDN is only offered when ingress is external. An internal ingress resolves inside the
    /// managed environment''s network, so handing it to an operator as an application URL would produce a
    /// project whose health check can never succeed.
    /// </para>
    /// <para>
    /// https is assumed because both platforms terminate TLS on the assigned host name and neither serves
    /// plain http there.
    /// </para>
    /// </summary>
    private static string? ComposeApplicationUrl(ResourceGraphRow row)
    {
        string? host = NullIfWhiteSpace(row.HostName);
        if (host is null)
        {
            return null;
        }

        bool reachable = ParsePlatform(row.Platform) == AzureLogPlatform.AppService
            || string.Equals(row.IngressExternal?.Trim(), "true", StringComparison.OrdinalIgnoreCase);

        return reachable ? $"https://{host}" : null;
    }

    private static string? NullIfWhiteSpace(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private sealed record ResourceGraphRequest(
        [property: JsonPropertyName("query")] string Query,
        [property: JsonPropertyName("options")] ResourceGraphRequestOptions Options);

    private sealed record ResourceGraphRequestOptions(
        [property: JsonPropertyName("$top")] int Top,
        [property: JsonPropertyName("allowPartialScopes")] bool AllowPartialScopes);

    private sealed record ResourceGraphResponse(        ResourceGraphRow[]? Data,
        long TotalRecords,
        [property: JsonPropertyName("resultTruncated")] string? ResultTruncated);

    private sealed record ResourceGraphRow(
        string? Name,
        string? Platform,
        string? ResourceGroup,
        string? SubscriptionId,
        string? Location,
        string? EnvironmentName,
        string? WorkspaceId,
        string? HostName,
        string? IngressExternal);
}
