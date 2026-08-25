using ConsoleOps.Application.Features.Authentication;
using Microsoft.Extensions.DependencyInjection;

namespace ConsoleOps.Api.Security;

/// <summary>
/// Requires a signed-in operator, or the configured key, on the API surface.
/// </summary>
/// <remarks>
/// <para>
/// Replaces the key-only check. Both are accepted: sign-in is how a person reaches Console Ops through a browser,
/// and the key remains the way something that is not a person - a script, a probe - reaches it without a session.
/// </para>
/// <para>
/// A session is renewed here when its access token is close to expiring, because the request that follows may read
/// GitHub as this operator and a token that dies mid-request would surface as a provider failure. That is a database
/// write only inside the renewal window, not on every request, and doing it here - once, before anything fans out -
/// means concurrent provider reads never race to renew the same session.
/// </para>
/// </remarks>
public sealed class ApiAuthenticationMiddleware(RequestDelegate next, IConfiguration configuration)
{
    private readonly string? configuredKey = configuration["Api:Key"];
    private readonly bool signInConfigured =
        !string.IsNullOrWhiteSpace(configuration["GitHub:App:ClientId"])
        && !string.IsNullOrWhiteSpace(configuration["GitHub:App:ClientSecret"]);

    public async Task InvokeAsync(HttpContext context)
    {
        if (!ApiAccess.RequiresAuthentication(signInConfigured, configuredKey, context.Request.Path))
        {
            await next(context);
            return;
        }

        if (ApiKeyAuthentication.IsKeyValid(configuredKey, context.Request.Headers[ApiKeyAuthentication.HeaderName]))
        {
            await next(context);
            return;
        }

        if (await IsSignedInAsync(context))
        {
            await next(context);
            return;
        }

        // The response says what is required and nothing about who is signed in elsewhere, which key was expected,
        // or whether the account that tried is an operator.
        await Results.Problem(
                statusCode: StatusCodes.Status401Unauthorized,
                title: "Unauthorized",
                detail: signInConfigured
                    ? "Sign in with GitHub to use this Console Ops."
                    : $"This API requires the {ApiKeyAuthentication.HeaderName} header.",
                extensions: new Dictionary<string, object?>
                {
                    ["code"] = signInConfigured ? "Auth.SignInRequired" : "Api.KeyRequired",
                })
            .ExecuteAsync(context);
    }

    /// <summary>
    /// Whether the request carries a session that is still an operator's.
    /// </summary>
    /// <remarks>
    /// The allow list is consulted here too, so removing somebody takes effect on their next request rather than
    /// when their token happens to expire.
    /// </remarks>
    private static async Task<bool> IsSignedInAsync(HttpContext context)
    {
        Guid? sessionId = SessionCookie.Read(context.Request);
        if (sessionId is null)
        {
            return false;
        }

        IOperatorSessionStore sessions = context.RequestServices.GetRequiredService<IOperatorSessionStore>();
        OperatorSession? session = await sessions.FindAsync(sessionId.Value, context.RequestAborted);
        if (session is null)
        {
            return false;
        }

        OperatorAllowList allowList = context.RequestServices.GetRequiredService<OperatorAllowList>();
        if (!allowList.Admits(session.Login))
        {
            return false;
        }

        TimeProvider time = context.RequestServices.GetRequiredService<TimeProvider>();
        DateTimeOffset now = time.GetUtcNow();

        // A session whose refresh token has expired cannot be renewed, so it is over.
        if (session.RefreshTokenExpiresAtUtc is { } refreshExpiry && refreshExpiry <= now)
        {
            return false;
        }

        // An access token near expiry is renewed now rather than allowed to die during the request it is about to
        // serve. A session that cannot be renewed is not signed in.
        OperatorSessionRefresher refresher = context.RequestServices
            .GetRequiredService<OperatorSessionRefresher>();
        OperatorSession? fresh = await refresher.EnsureFreshAsync(session, context.RequestAborted);
        if (fresh is null)
        {
            return false;
        }

        // Carried forward so the GitHub credential can act as this operator without reading the session again.
        OperatorRequestContext.Set(context, fresh);
        return true;
    }
}
