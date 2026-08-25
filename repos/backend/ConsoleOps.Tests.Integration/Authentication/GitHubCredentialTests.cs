using System.Net;
using System.Net.Http.Headers;
using ConsoleOps.Api.Security;
using ConsoleOps.Application.Features.Authentication;
using ConsoleOps.Application.Integrations.GitHub;
using ConsoleOps.Infrastructure.Integrations.GitHub;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;

namespace ConsoleOps.Tests.Integration.Authentication;

/// <summary>
/// Whose token goes to GitHub. The reason sign-in exists: an operator's request must read with their access, and
/// scheduled collection must keep reading with Console Ops' own.
/// </summary>
public sealed class GitHubCredentialTests
{
    private static readonly DateTimeOffset Now = new(2026, 8, 25, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task Sends_the_signed_in_operators_token()
    {
        DefaultHttpContext context = new();
        OperatorRequestContext.Set(context, Session("operator-token"));

        AuthenticationHeaderValue? sent = await SendThroughHandler(Credential(context));

        Assert.NotNull(sent);
        Assert.Equal("Bearer", sent.Scheme);
        Assert.Equal("operator-token", sent.Parameter);
    }

    /// <summary>A caller holding the API key has no operator, so Console Ops reads as itself.</summary>
    [Fact]
    public async Task Sends_the_service_token_for_a_request_with_no_operator()
    {
        DefaultHttpContext context = new();

        AuthenticationHeaderValue? sent = await SendThroughHandler(Credential(context));

        Assert.Equal("service-token", sent?.Parameter);
    }

    /// <summary>Scheduled collection runs outside any request and keeps its configured token.</summary>
    [Fact]
    public async Task Sends_the_service_token_when_there_is_no_request()
    {
        AuthenticationHeaderValue? sent = await SendThroughHandler(Credential(httpContext: null));

        Assert.Equal("service-token", sent?.Parameter);
    }

    [Fact]
    public async Task Sends_no_credential_when_none_is_configured()
    {
        AuthenticationHeaderValue? sent = await SendThroughHandler(new StubCredential(null));

        Assert.Null(sent);
    }

    /// <summary>A call that already chose a token keeps it, so the handler cannot override a deliberate choice.</summary>
    [Fact]
    public async Task Leaves_a_credential_the_caller_already_set()
    {
        AuthenticationHeaderValue? sent = await SendThroughHandler(
            new StubCredential("operator-token"),
            preset: new AuthenticationHeaderValue("Bearer", "explicit-token"));

        Assert.Equal("explicit-token", sent?.Parameter);
    }

    private static async Task<AuthenticationHeaderValue?> SendThroughHandler(
        IGitHubCredential credential,
        AuthenticationHeaderValue? preset = null)
    {
        CapturingHandler inner = new();
        GitHubAuthorizationHandler handler = new(credential) { InnerHandler = inner };
        using HttpMessageInvoker invoker = new(handler);
        using HttpRequestMessage request = new(HttpMethod.Get, "https://api.github.test/repos");
        request.Headers.Authorization = preset;

        using HttpResponseMessage response = await invoker.SendAsync(request, CancellationToken.None);

        return inner.Authorization;
    }

    private static OperatorGitHubCredential Credential(HttpContext? httpContext) =>
        new(new StubHttpContextAccessor(httpContext), ServiceCredential());

    private static ConfiguredGitHubCredential ServiceCredential() =>
        new(new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["GitHub:Token"] = "service-token" })
            .Build());

    private static OperatorSession Session(string accessToken) => new(
        Guid.CreateVersion7(),
        12345,
        "Clinttttt",
        null,
        accessToken,
        Now.AddHours(6),
        "stored-refresh",
        Now.AddMonths(5),
        Now.AddMinutes(-30),
        Now.AddMinutes(-1));

    private sealed class CapturingHandler : HttpMessageHandler
    {
        public AuthenticationHeaderValue? Authorization { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            Authorization = request.Headers.Authorization;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK));
        }
    }

    private sealed class StubHttpContextAccessor(HttpContext? context) : IHttpContextAccessor
    {
        public HttpContext? HttpContext { get; set; } = context;
    }

    private sealed class StubCredential(string? token) : IGitHubCredential
    {
        public ValueTask<string?> GetTokenAsync(CancellationToken cancellationToken) =>
            ValueTask.FromResult(token);
    }
}
