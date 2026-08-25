using System.Net.Http.Headers;
using ConsoleOps.Api.Security;
using ConsoleOps.Application.Features.Authentication;
using ConsoleOps.Application.Integrations.GitHub;
using ConsoleOps.Tests.Integration.Infrastructure;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace ConsoleOps.Tests.Integration.Authentication;

/// <summary>
/// That the credential is actually wired into the GitHub clients.
/// </summary>
/// <remarks>
/// The other tests in this project replace the GitHub ports with fakes, so none of them send a real HTTP message and
/// none would notice the authorization handler being dropped from a client - which would leave every read
/// unauthenticated. These build the app's own handler chain and stub only the socket underneath it.
/// </remarks>
[Collection(ConsoleOpsApiCollection.Name)]
public sealed class GitHubClientAuthorizationTests(ConsoleOpsApiFactory factory)
{
    private static readonly DateTimeOffset FarFuture = DateTimeOffset.UtcNow.AddHours(6);

    [Theory]
    [InlineData(nameof(IGitHubProjectReader))]
    [InlineData(nameof(IGitHubRepositoryCatalog))]
    [InlineData(nameof(IGitHubWorkflowInventory))]
    public async Task Every_GitHub_client_sends_the_operators_token(string clientName)
    {
        CapturedRequest captured = new();
        using WebApplicationFactory<Program> application = Application(clientName, captured);

        OperatorSession session = await StoreSessionAsync(application, "operator-live-token");
        await SendThroughClientAsync(application, clientName, session);

        Assert.Equal("Bearer", captured.Authorization?.Scheme);
        Assert.Equal("operator-live-token", captured.Authorization?.Parameter);
    }

    /// <summary>Without an operator the service token is still sent, so scheduled collection keeps working.</summary>
    [Fact]
    public async Task A_client_used_outside_a_request_sends_the_service_token()
    {
        CapturedRequest captured = new();
        using WebApplicationFactory<Program> application = Application(
            nameof(IGitHubWorkflowInventory),
            captured,
            serviceToken: "configured-service-token");

        await SendThroughClientAsync(application, nameof(IGitHubWorkflowInventory), session: null);

        Assert.Equal("configured-service-token", captured.Authorization?.Parameter);
    }

    private WebApplicationFactory<Program> Application(
        string clientName,
        CapturedRequest captured,
        string? serviceToken = null) =>
        factory.WithWebHostBuilder(builder =>
        {
            if (serviceToken is not null)
            {
                builder.UseSetting("GitHub:Token", serviceToken);
            }

            builder.ConfigureServices(services =>
                // Configures the client the app already registered under this name, so the authorization handler
                // stays in the chain and only the transport is replaced.
                services.AddHttpClient(clientName)
                    .ConfigurePrimaryHttpMessageHandler(() => new CapturingHandler(captured)));
        });

    private static async Task<OperatorSession> StoreSessionAsync(
        WebApplicationFactory<Program> application,
        string accessToken)
    {
        OperatorSession session = new(
            Guid.CreateVersion7(),
            12345,
            "Clinttttt",
            null,
            accessToken,
            FarFuture,
            "stored-refresh",
            DateTimeOffset.UtcNow.AddMonths(5),
            DateTimeOffset.UtcNow.AddMinutes(-30),
            DateTimeOffset.UtcNow.AddMinutes(-1));

        await using AsyncServiceScope scope = application.Services.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<IOperatorSessionStore>().SaveAsync(session, default);

        return session;
    }

    /// <summary>
    /// Sends one request through the named client, as the given operator when there is one.
    /// </summary>
    /// <remarks>
    /// The handler chain is built by name rather than by resolving an adapter, because what is under test is the
    /// pipeline the app configured, not any one adapter's calls.
    /// </remarks>
    private static async Task SendThroughClientAsync(
        WebApplicationFactory<Program> application,
        string clientName,
        OperatorSession? session)
    {
        await using AsyncServiceScope scope = application.Services.CreateAsyncScope();

        IHttpContextAccessor accessor = application.Services.GetRequiredService<IHttpContextAccessor>();
        if (session is null)
        {
            accessor.HttpContext = null;
        }
        else
        {
            DefaultHttpContext context = new() { RequestServices = scope.ServiceProvider };
            OperatorRequestContext.Set(context, session);
            accessor.HttpContext = context;
        }

        try
        {
            IHttpMessageHandlerFactory handlers = application.Services
                .GetRequiredService<IHttpMessageHandlerFactory>();
            using HttpMessageInvoker invoker = new(handlers.CreateHandler(clientName), disposeHandler: false);
            using HttpRequestMessage request = new(HttpMethod.Get, "https://api.github.com/user");

            using HttpResponseMessage response = await invoker.SendAsync(request, CancellationToken.None);
        }
        finally
        {
            accessor.HttpContext = null;
        }
    }

    private sealed class CapturedRequest
    {
        public AuthenticationHeaderValue? Authorization { get; set; }
    }

    private sealed class CapturingHandler(CapturedRequest captured) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            captured.Authorization = request.Headers.Authorization;
            return Task.FromResult(new HttpResponseMessage(System.Net.HttpStatusCode.OK));
        }
    }
}
