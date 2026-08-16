using System.Collections.Concurrent;
using System.Net;
using System.Text;
using Azure.Core;
using ConsoleOps.Application.Integrations.AzureMonitor;
using ConsoleOps.Infrastructure.Integrations.AzureMonitor;

namespace ConsoleOps.Tests.Integration.AzureMonitor;

/// <summary>
/// Discovery is read-only inventory. These tests pin the request it makes, what it recovers, and that a
/// failure to ask is never reported as an empty tenant.
/// </summary>
public sealed class AzureResourceGraphCatalogTests
{
    private static readonly Guid Workspace = Guid.Parse("6f5c1a2b-3d4e-5f60-7182-93a4b5c6d7e8");

    [Fact]
    public async Task ListContainerApps_ReadsInventoryAndTheWorkspaceEachAppLogsTo()
    {
        RecordingHandler handler = new(_ => JsonResponse($$"""
            {
              "totalRecords": 2,
              "count": 2,
              "resultTruncated": "false",
              "data": [
                {
                  "name": "spinner-api",
                  "resourceGroup": "spinner-rg",
                  "subscriptionId": "11111111-2222-3333-4444-555555555555",
                  "location": "southeastasia",
                  "environmentName": "spinner-env",
                  "workspaceId": "{{Workspace}}"
                },
                {
                  "name": "no-logs-app",
                  "resourceGroup": "spinner-rg",
                  "subscriptionId": "11111111-2222-3333-4444-555555555555",
                  "location": "southeastasia",
                  "environmentName": "bare-env",
                  "workspaceId": ""
                }
              ]
            }
            """));
        IAzureLogSourceCatalog catalog = CreateCatalog(handler);

        AzureLogSourceCatalogResult result = await catalog.ListContainerAppsAsync(null, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.False(result.HasMore);
        Assert.Collection(
            result.ContainerApps,
            app =>
            {
                Assert.Equal("spinner-api", app.Name);
                Assert.Equal("spinner-rg", app.ResourceGroup);
                Assert.Equal("spinner-env", app.EnvironmentName);
                Assert.Equal(Workspace, app.WorkspaceId);
            },
            app =>
            {
                // An environment with no log configuration has no workspace, which is a fact, not an error.
                Assert.Equal("no-logs-app", app.Name);
                Assert.Null(app.WorkspaceId);
            });

        CapturedRequest request = Assert.Single(handler.Requests);
        Assert.Equal(HttpMethod.Post, request.Method);
        Assert.Equal(
            $"/providers/Microsoft.ResourceGraph/resources?api-version={AzureResourceGraphCatalog.ApiVersion}",
            request.Uri.PathAndQuery);
        Assert.Equal("Bearer test-token", request.Authorization);

        string sentQuery = System.Text.Json.JsonDocument.Parse(request.Body)
            .RootElement.GetProperty("query").GetString()!;
        // Read-only inventory over the two resource types, bounded to a page.
        Assert.Contains("microsoft.app/containerapps", sentQuery, StringComparison.Ordinal);
        Assert.Contains("microsoft.app/managedenvironments", sentQuery, StringComparison.Ordinal);
        Assert.Contains("limit 200", sentQuery, StringComparison.Ordinal);
        Assert.DoesNotContain("| where name contains", sentQuery, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ListContainerApps_WithAFilter_SendsItAsAnEscapedLiteral()
    {
        RecordingHandler handler = new(_ => JsonResponse("""{ "totalRecords": 0, "data": [] }"""));
        IAzureLogSourceCatalog catalog = CreateCatalog(handler);

        await catalog.ListContainerAppsAsync(" spin\"ner ", CancellationToken.None);

        CapturedRequest request = Assert.Single(handler.Requests);
        // Read the query as the provider would, so JSON escaping cannot hide what was sent.
        string sentQuery = System.Text.Json.JsonDocument.Parse(request.Body)
            .RootElement.GetProperty("query").GetString()!;
        // The quote is escaped inside the literal rather than closing it.
        Assert.Contains("name contains \"spin\\\"ner\"", sentQuery, StringComparison.Ordinal);
        Assert.Contains("resourceGroup contains \"spin\\\"ner\"", sentQuery, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ListContainerApps_WhenAzureTruncates_SaysTheListIsNotEverything()
    {
        RecordingHandler handler = new(_ => JsonResponse("""
            { "totalRecords": 900, "count": 200, "resultTruncated": "true", "data": [] }
            """));
        IAzureLogSourceCatalog catalog = CreateCatalog(handler);

        AzureLogSourceCatalogResult result = await catalog.ListContainerAppsAsync(null, CancellationToken.None);

        Assert.True(result.HasMore);
    }

    [Theory]
    [InlineData(HttpStatusCode.Unauthorized, AzureCatalogFailure.Unauthorized)]
    [InlineData(HttpStatusCode.Forbidden, AzureCatalogFailure.Unauthorized)]
    [InlineData(HttpStatusCode.NotFound, AzureCatalogFailure.NotFound)]
    [InlineData(HttpStatusCode.TooManyRequests, AzureCatalogFailure.RateLimited)]
    [InlineData(HttpStatusCode.BadRequest, AzureCatalogFailure.InvalidResponse)]
    [InlineData(HttpStatusCode.ServiceUnavailable, AzureCatalogFailure.Unavailable)]
    public async Task ListContainerApps_MapsProviderFailuresWithoutLeakingTheBody(
        HttpStatusCode status,
        AzureCatalogFailure expected)
    {
        RecordingHandler handler = new(_ => new HttpResponseMessage(status)
        {
            Content = new StringContent("sensitive provider details"),
        });
        IAzureLogSourceCatalog catalog = CreateCatalog(handler);

        AzureLogSourceCatalogResult result = await catalog.ListContainerAppsAsync(null, CancellationToken.None);

        // Failing to ask is never an empty tenant.
        Assert.False(result.IsSuccess);
        Assert.Equal(expected, result.Failure);
        Assert.Empty(result.ContainerApps);
    }

    [Fact]
    public async Task ListContainerApps_WhenNoIdentityIsSignedIn_ReportsUnauthorized()
    {
        RecordingHandler handler = new(_ => JsonResponse("""{ "data": [] }"""));
        IAzureLogSourceCatalog catalog = new AzureResourceGraphCatalog(
            CreateClient(handler),
            new UnavailableCredential());

        AzureLogSourceCatalogResult result = await catalog.ListContainerAppsAsync(null, CancellationToken.None);

        Assert.Equal(AzureCatalogFailure.Unauthorized, result.Failure);
        // Nothing was sent, because there was nothing to authenticate with.
        Assert.Empty(handler.Requests);
    }

    [Fact]
    public async Task ListContainerApps_WhenPayloadIsMalformed_ReportsAnInvalidResponse()
    {
        RecordingHandler handler = new(_ => JsonResponse("{not-json"));
        IAzureLogSourceCatalog catalog = CreateCatalog(handler);

        AzureLogSourceCatalogResult result = await catalog.ListContainerAppsAsync(null, CancellationToken.None);

        Assert.Equal(AzureCatalogFailure.InvalidResponse, result.Failure);
    }

    private static IAzureLogSourceCatalog CreateCatalog(HttpMessageHandler handler) =>
        new AzureResourceGraphCatalog(CreateClient(handler), new StubCredential());

    private static HttpClient CreateClient(HttpMessageHandler handler) =>
        new(handler)
        {
            BaseAddress = new Uri("https://management.azure.test/"),
            Timeout = TimeSpan.FromSeconds(5),
        };

    private static HttpResponseMessage JsonResponse(string json) => new(HttpStatusCode.OK)
    {
        Content = new StringContent(json, Encoding.UTF8, "application/json"),
    };

    private sealed class StubCredential : TokenCredential
    {
        public override AccessToken GetToken(TokenRequestContext requestContext, CancellationToken cancellationToken) =>
            new("test-token", DateTimeOffset.UtcNow.AddHours(1));

        public override ValueTask<AccessToken> GetTokenAsync(
            TokenRequestContext requestContext,
            CancellationToken cancellationToken) =>
            new(GetToken(requestContext, cancellationToken));
    }

    private sealed class UnavailableCredential : TokenCredential
    {
        public override AccessToken GetToken(TokenRequestContext requestContext, CancellationToken cancellationToken) =>
            throw new Azure.Identity.CredentialUnavailableException("No Azure identity is available.");

        public override ValueTask<AccessToken> GetTokenAsync(
            TokenRequestContext requestContext,
            CancellationToken cancellationToken) =>
            throw new Azure.Identity.CredentialUnavailableException("No Azure identity is available.");
    }

    private sealed class RecordingHandler(Func<HttpRequestMessage, HttpResponseMessage> responder)
        : HttpMessageHandler
    {
        public ConcurrentQueue<CapturedRequest> Requests { get; } = new();

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            string body = request.Content is null
                ? string.Empty
                : await request.Content.ReadAsStringAsync(cancellationToken);
            Requests.Enqueue(new CapturedRequest(
                request.Method,
                request.RequestUri!,
                request.Headers.Authorization?.ToString() ?? string.Empty,
                body));

            return responder(request);
        }
    }

    private sealed record CapturedRequest(HttpMethod Method, Uri Uri, string Authorization, string Body);
}
