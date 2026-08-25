using ConsoleOps.Application.Features.Authentication;
using ConsoleOps.Application.Integrations.GitHub;
using ConsoleOps.Infrastructure.Integrations.GitHub;

namespace ConsoleOps.Api.Security;

/// <summary>
/// Reads GitHub as the operator who made the request, and as the service otherwise.
/// </summary>
/// <remarks>
/// <para>
/// The point of signing in: GitHub applies the access of the person asking, and a workflow they start is attributed
/// to them rather than to whoever's token Console Ops was configured with.
/// </para>
/// <para>
/// An operator's request never falls back to the service token. Doing so would quietly read repositories through a
/// credential the operator does not have, which is a privilege escalation dressed as resilience: the request is
/// better refused by GitHub than served by the wrong identity. The service token is used only where there is no
/// operator at all - scheduled collection, or a caller holding the API key.
/// </para>
/// <para>
/// A singleton holding no state of its own. The session was resolved by <see cref="ApiAuthenticationMiddleware"/>
/// and is already fresh, so this makes no database call and can be used from a pooled message handler safely.
/// </para>
/// </remarks>
public sealed class OperatorGitHubCredential(
    IHttpContextAccessor httpContextAccessor,
    ConfiguredGitHubCredential serviceCredential) : IGitHubCredential
{
    public ValueTask<string?> GetTokenAsync(CancellationToken cancellationToken)
    {
        HttpContext? context = httpContextAccessor.HttpContext;
        if (context is null)
        {
            // No request: scheduled collection, which has no operator and keeps its own token.
            return serviceCredential.GetTokenAsync(cancellationToken);
        }

        OperatorSession? session = OperatorRequestContext.Get(context);
        return session is null
            ? serviceCredential.GetTokenAsync(cancellationToken)
            : ValueTask.FromResult<string?>(session.AccessToken);
    }
}
