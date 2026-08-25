using System.Net.Http.Headers;
using ConsoleOps.Application.Integrations.GitHub;

namespace ConsoleOps.Infrastructure.Integrations.GitHub;

/// <summary>
/// Puts the current GitHub credential on every outbound GitHub request.
/// </summary>
/// <remarks>
/// <para>
/// Per request rather than per client, because the credential depends on who is asking. Setting it once when the
/// client is built - as this used to - meant every read used the service token no matter who was signed in.
/// </para>
/// <para>
/// An <c>Authorization</c> header already on the message is left alone, so a call that must use a specific token
/// still can.
/// </para>
/// </remarks>
public sealed class GitHubAuthorizationHandler(IGitHubCredential credential) : DelegatingHandler
{
    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        if (request.Headers.Authorization is null)
        {
            string? token = await credential.GetTokenAsync(cancellationToken);
            if (!string.IsNullOrWhiteSpace(token))
            {
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            }
        }

        return await base.SendAsync(request, cancellationToken);
    }
}
