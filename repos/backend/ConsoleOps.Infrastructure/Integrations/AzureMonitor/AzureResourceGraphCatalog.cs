using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Azure.Core;
using ConsoleOps.Application.Integrations.AzureMonitor;
using ConsoleOps.Domain.Projects;

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

    /// <summary>Version that exposes a diagnostic setting''s category list, which is what identifies a console sink.</summary>
    internal const string DiagnosticSettingsApiVersion = "2021-05-01-preview";

    internal const string ConsoleLogCategory = "AppServiceConsoleLogs";

    /// <summary>
    /// How many sites are asked about at once. A page can hold many, and a picker opening should not turn into a
    /// burst of requests against the management API.
    /// </summary>
    private const int MaximumConcurrentSiteReads = 4;

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

            // App Service sites arrive without a workspace, because a site's destination lives in a diagnostic
            // setting and Resource Graph does not expose those. Resolved here rather than left null: a source whose
            // workspace is unknown cannot be offered, and the operator would otherwise have to find the GUID and
            // the platform by hand - which they cannot even do, since the platform is only set by choosing here.
            sources = await ResolveAppServiceWorkspacesAsync(sources, token, cancellationToken);

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

    /// <summary>
    /// Fills in the workspace for App Service sites by reading each site's diagnostic settings.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Two steps, because neither source alone has the answer. A site's destination is a diagnostic setting, which
    /// only ARM can read - one call per site, bounded in flight so a page of sites cannot fan out without limit.
    /// That yields a workspace resource id, while the reader needs the workspace's customer GUID, so the ids are
    /// resolved together in a single Resource Graph query rather than one call each.
    /// </para>
    /// <para>
    /// A site whose setting cannot be read, or which sends its console logs nowhere, keeps a null workspace and the
    /// screen says so. Nothing is guessed: offering a workspace that turns out to be wrong would produce an empty
    /// window, which reads as a quiet application rather than as a misconfiguration.
    /// </para>
    /// </summary>
    private async Task<AzureLogSourceCandidate[]> ResolveAppServiceWorkspacesAsync(
        AzureLogSourceCandidate[] sources,
        AccessToken token,
        CancellationToken cancellationToken)
    {
        AzureLogSourceCandidate[] pending = [.. sources.Where(source =>
            source.Platform == AzureLogPlatform.AppService && source.WorkspaceId is null)];
        if (pending.Length == 0)
        {
            return sources;
        }

        using SemaphoreSlim inFlight = new(MaximumConcurrentSiteReads);
        Dictionary<string, string> workspaceIdBySite = new(StringComparer.OrdinalIgnoreCase);

        await Task.WhenAll(pending.Select(async source =>
        {
            await inFlight.WaitAsync(cancellationToken);
            try
            {
                string? workspaceResourceId = await ReadConsoleLogWorkspaceAsync(source, token, cancellationToken);
                if (workspaceResourceId is not null)
                {
                    lock (workspaceIdBySite)
                    {
                        workspaceIdBySite[SiteKey(source)] = workspaceResourceId;
                    }
                }
            }
            finally
            {
                inFlight.Release();
            }
        }));

        if (workspaceIdBySite.Count == 0)
        {
            return sources;
        }

        Dictionary<string, Guid> customerIds = await ReadWorkspaceCustomerIdsAsync(
            workspaceIdBySite.Values.Distinct(StringComparer.OrdinalIgnoreCase).ToArray(),
            token,
            cancellationToken);

        return [.. sources.Select(source =>
            source.WorkspaceId is null
            && workspaceIdBySite.TryGetValue(SiteKey(source), out string? resourceId)
            && customerIds.TryGetValue(resourceId, out Guid customerId)
                ? source with { WorkspaceId = customerId }
                : source)];
    }

    /// <summary>
    /// The workspace a site sends console logs to, or <c>null</c> when it sends them nowhere Console Ops can read.
    /// </summary>
    /// <remarks>
    /// Only a setting with <c>AppServiceConsoleLogs</c> enabled counts. A site may send other categories - HTTP or
    /// audit logs - to a workspace that holds no console output, and offering that would produce an empty stream.
    /// </remarks>
    private async Task<string?> ReadConsoleLogWorkspaceAsync(
        AzureLogSourceCandidate source,
        AccessToken token,
        CancellationToken cancellationToken)
    {
        string url =
            $"subscriptions/{source.SubscriptionId}/resourceGroups/{source.ResourceGroup}"
            + $"/providers/Microsoft.Web/sites/{Uri.EscapeDataString(source.Name)}"
            + $"/providers/microsoft.insights/diagnosticSettings?api-version={DiagnosticSettingsApiVersion}";

        try
        {
            using HttpRequestMessage request = new(HttpMethod.Get, url);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token.Token);

            using HttpResponseMessage response = await httpClient.SendAsync(
                request,
                HttpCompletionOption.ResponseHeadersRead,
                cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                return null;
            }

            await using Stream stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            DiagnosticSettingsResponse? payload = await JsonSerializer.DeserializeAsync<DiagnosticSettingsResponse>(
                stream,
                SerializerOptions,
                cancellationToken);

            return payload?.Value
                ?.Where(setting => setting.Properties?.Logs?.Any(log =>
                    log.Enabled
                    && string.Equals(log.Category, ConsoleLogCategory, StringComparison.OrdinalIgnoreCase)) == true)
                .Select(setting => NullIfWhiteSpace(setting.Properties?.WorkspaceId))
                .FirstOrDefault(workspaceId => workspaceId is not null);
        }
        catch (HttpRequestException)
        {
            // A site whose settings could not be read keeps an unknown workspace, which the screen reports.
            return null;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    /// <summary>
    /// Maps workspace resource ids to the customer GUIDs a log query is addressed with, in one round trip.
    /// </summary>
    private async Task<Dictionary<string, Guid>> ReadWorkspaceCustomerIdsAsync(
        string[] resourceIds,
        AccessToken token,
        CancellationToken cancellationToken)
    {
        Dictionary<string, Guid> customerIds = new(StringComparer.OrdinalIgnoreCase);

        try
        {
            using HttpRequestMessage request = new(
                HttpMethod.Post,
                $"providers/Microsoft.ResourceGraph/resources?api-version={ApiVersion}")
            {
                Content = JsonContent.Create(new ResourceGraphRequest(
                    AzureLogSourceDiscoveryQuery.BuildWorkspaceLookup(resourceIds),
                    new ResourceGraphRequestOptions(resourceIds.Length, true))),
            };
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token.Token);

            using HttpResponseMessage response = await httpClient.SendAsync(
                request,
                HttpCompletionOption.ResponseHeadersRead,
                cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                return customerIds;
            }

            await using Stream stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            WorkspaceLookupResponse? payload = await JsonSerializer.DeserializeAsync<WorkspaceLookupResponse>(
                stream,
                SerializerOptions,
                cancellationToken);

            foreach (WorkspaceLookupRow row in payload?.Data ?? [])
            {
                if (!string.IsNullOrWhiteSpace(row.Id) && ParseWorkspaceId(row.CustomerId) is { } customerId)
                {
                    customerIds[row.Id.Trim()] = customerId;
                }
            }
        }
        catch (HttpRequestException)
        {
            // Nothing resolved: every affected site keeps an unknown workspace rather than a guessed one.
        }
        catch (JsonException)
        {
        }

        return customerIds;
    }

    private static string SiteKey(AzureLogSourceCandidate source) =>
        $"{source.SubscriptionId}/{source.ResourceGroup}/{source.Name}";

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

file sealed record DiagnosticSettingsResponse(DiagnosticSetting[]? Value);

file sealed record DiagnosticSetting(DiagnosticSettingProperties? Properties);

file sealed record DiagnosticSettingProperties(string? WorkspaceId, DiagnosticSettingLog[]? Logs);

file sealed record DiagnosticSettingLog(string? Category, bool Enabled);

file sealed record WorkspaceLookupResponse(WorkspaceLookupRow[]? Data);

file sealed record WorkspaceLookupRow(string? Id, string? CustomerId);