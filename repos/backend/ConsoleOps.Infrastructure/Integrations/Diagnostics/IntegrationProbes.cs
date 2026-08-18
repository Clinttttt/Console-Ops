using Azure.Core;
using ConsoleOps.Application.Integrations.Diagnostics;
using ConsoleOps.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ConsoleOps.Infrastructure.Integrations.Diagnostics;

/// <summary>
/// Opens a connection to Console Ops' own database.
/// </summary>
internal sealed class DatabaseProbe(ConsoleOpsDbContext dbContext) : IIntegrationProbe
{
    public string Capability => "Database";

    public async Task<IntegrationProbeResult> ProbeAsync(CancellationToken cancellationToken)
    {
        try
        {
            return await dbContext.Database.CanConnectAsync(cancellationToken)
                ? IntegrationProbeResult.Success()
                : IntegrationProbeResult.Failed("The database refused the connection.");
        }
        catch (Exception failure) when (failure is not OperationCanceledException)
        {
            // The exception text can carry the host and user from a connection string, so it is not repeated.
            return IntegrationProbeResult.Failed("The database could not be reached.");
        }
    }
}

/// <summary>
/// Asks GitHub who the configured token belongs to, which is the cheapest call that proves it is valid.
/// </summary>
internal sealed class GitHubTokenProbe(IHttpClientFactory httpClientFactory) : IIntegrationProbe
{
    public string Capability => "Source and CI";

    public async Task<IntegrationProbeResult> ProbeAsync(CancellationToken cancellationToken)
    {
        try
        {
            using HttpClient client = httpClientFactory.CreateClient(nameof(GitHubTokenProbe));
            using HttpResponseMessage response = await client.GetAsync("rate_limit", cancellationToken);

            return response.StatusCode switch
            {
                System.Net.HttpStatusCode.OK => IntegrationProbeResult.Success(),
                System.Net.HttpStatusCode.Unauthorized =>
                    IntegrationProbeResult.Failed("GitHub rejected the token."),
                System.Net.HttpStatusCode.Forbidden =>
                    IntegrationProbeResult.Failed("The token is valid but lacks the required access."),
                _ => IntegrationProbeResult.Failed($"GitHub answered {(int)response.StatusCode}.")
            };
        }
        catch (Exception failure) when (failure is not OperationCanceledException)
        {
            return IntegrationProbeResult.Failed("GitHub could not be reached.");
        }
    }
}

/// <summary>
/// Asks the configured Azure identity for a management token, which is what every Azure read depends on.
/// </summary>
internal sealed class AzureCredentialProbe(TokenCredential credential) : IIntegrationProbe
{
    private const string ManagementScope = "https://management.azure.com/.default";

    public string Capability => "Azure";

    public async Task<IntegrationProbeResult> ProbeAsync(CancellationToken cancellationToken)
    {
        try
        {
            AccessToken token = await credential.GetTokenAsync(
                new TokenRequestContext([ManagementScope]),
                cancellationToken);

            // The token itself is never inspected beyond whether one was issued and is still valid.
            return token.ExpiresOn > DateTimeOffset.UtcNow
                ? IntegrationProbeResult.Success()
                : IntegrationProbeResult.Failed("Azure returned an expired token.");
        }
        catch (Exception failure) when (failure is not OperationCanceledException)
        {
            return IntegrationProbeResult.Failed(
                "Azure did not issue a token. Sign in with az login, or configure Azure:TenantId, "
                + "Azure:ClientId and Azure:ClientSecret.");
        }
    }
}
