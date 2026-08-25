using ConsoleOps.Application.Integrations.GitHub;
using ConsoleOps.Tests.Integration.Infrastructure;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace ConsoleOps.Tests.Integration.Authentication;

/// <summary>
/// What the browser is left looking at when a sign-in cannot be completed.
/// </summary>
/// <remarks>
/// A browser arrives at the callback mid-redirect, having already authorized on GitHub, so this endpoint must always
/// end in a redirect. A missing database table once let an exception reach the global handler and stranded the
/// operator on a problem document in the address bar with no way back.
/// </remarks>
[Collection(ConsoleOpsApiCollection.Name)]
public sealed class SignInCallbackTests(ConsoleOpsApiFactory factory)
{
    [Fact]
    public async Task An_unexpected_fault_returns_the_operator_to_the_sign_in_screen()
    {
        using WebApplicationFactory<Program> application = factory.WithWebHostBuilder(builder =>
        {
            // An operator has to be configured to get past the allow list, which is checked before any code is
            // exchanged - so reaching the fault at all depends on that ordering.
            builder.UseSetting("Auth:AllowedGitHubLogins:0", "Clinttttt");
            builder.ConfigureServices(services =>
                services.Replace(ServiceDescriptor.Singleton<IGitHubUserAuthentication, FaultingAuthentication>()));
        });

        using HttpClient client = Client(application);
        const string state = "5B334A42D18A0B66BC6DDCE96415EEBC";
        client.DefaultRequestHeaders.Add("Cookie", $"consoleops_signin_state={state}");

        using HttpResponseMessage response = await client.GetAsync(
            $"/api/auth/github/callback?code=a-real-looking-code&state={state}");

        Assert.Equal(System.Net.HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal("/sign-in?error=unavailable", response.Headers.Location?.ToString());
    }

    /// <summary>A callback that cannot be tied to a sign-in this Console Ops started is refused before any exchange.</summary>
    [Fact]
    public async Task A_callback_without_a_matching_state_is_refused()
    {
        using WebApplicationFactory<Program> application = factory.WithWebHostBuilder(builder =>
            builder.ConfigureServices(services =>
                services.Replace(ServiceDescriptor.Singleton<IGitHubUserAuthentication, FaultingAuthentication>())));

        using HttpClient client = Client(application);

        using HttpResponseMessage response = await client.GetAsync(
            "/api/auth/github/callback?code=a-real-looking-code&state=not-the-state-we-issued");

        Assert.Equal(System.Net.HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal("/sign-in?error=state", response.Headers.Location?.ToString());
    }

    private static HttpClient Client(WebApplicationFactory<Program> application) =>
        application.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

    /// <summary>Stands in for anything that can fault while completing a sign-in.</summary>
    private sealed class FaultingAuthentication : IGitHubUserAuthentication
    {
        public Uri BuildAuthorizationUrl(string state, string redirectUri) => new("https://github.test/authorize");

        public Task<GitHubAuthenticationResult<GitHubUserToken>> ExchangeCodeAsync(
            string code,
            string redirectUri,
            CancellationToken cancellationToken) =>
            throw new InvalidOperationException("Something unexpected went wrong while completing the sign-in.");

        public Task<GitHubAuthenticationResult<GitHubUserToken>> RefreshAsync(
            string refreshToken,
            CancellationToken cancellationToken) =>
            throw new InvalidOperationException("Something unexpected went wrong while renewing the session.");

        public Task<GitHubAuthenticationResult<GitHubUserIdentity>> ReadUserAsync(
            string accessToken,
            CancellationToken cancellationToken) =>
            throw new InvalidOperationException("Something unexpected went wrong while reading the user.");
    }
}
