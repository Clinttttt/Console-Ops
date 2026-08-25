using System.Security.Cryptography;
using ConsoleOps.Api.Extensions;
using ConsoleOps.Api.Security;
using ConsoleOps.Application.Abstractions.Messaging;
using ConsoleOps.Application.Features.Authentication.GetSession;
using ConsoleOps.Application.Features.Authentication.SignIn;
using ConsoleOps.Application.Features.Authentication.SignOut;
using ConsoleOps.Application.Integrations.GitHub;
using MediatR;

namespace ConsoleOps.Api.Features.Authentication;

public static class AuthenticationEndpoints
{
    public static IEndpointRouteBuilder MapAuthenticationEndpoints(this IEndpointRouteBuilder endpoints)
    {
        RouteGroupBuilder auth = endpoints.MapGroup("/api/auth")
            .WithTags("Authentication");

        auth.MapStartSignInEndpoint();
        auth.MapSignInCallbackEndpoint();
        auth.MapGetSessionEndpoint();
        auth.MapSignOutEndpoint();
        return endpoints;
    }

    /// <summary>
    /// Where GitHub returns to after an operator authorizes.
    /// </summary>
    /// <remarks>
    /// Built from the request rather than configured, so the deployment that serves the browser is the one the code
    /// is issued to. On Vercel the browser only ever sees the Vercel origin - <c>/api</c> is rewritten to the
    /// container app - so this resolves to the Vercel domain and the cookie stays first-party.
    /// </remarks>
    internal static string CallbackUrl(HttpRequest request, IConfiguration configuration)
    {
        string? configured = configuration["Auth:CallbackUrl"];
        if (!string.IsNullOrWhiteSpace(configured))
        {
            return configured.Trim();
        }

        string forwardedHost = request.Headers["X-Forwarded-Host"].ToString();
        string host = string.IsNullOrWhiteSpace(forwardedHost) ? request.Host.Value ?? string.Empty : forwardedHost;
        string scheme = request.Headers["X-Forwarded-Proto"].ToString() is { Length: > 0 } forwardedScheme
            ? forwardedScheme
            : request.Scheme;

        return $"{scheme}://{host}/api/auth/github/callback";
    }
}

internal static class StartSignInEndpoint
{
    public static RouteGroupBuilder MapStartSignInEndpoint(this RouteGroupBuilder auth)
    {
        auth.MapGet("/github/start", Handle)
            .WithName("StartGitHubSignIn")
            .WithSummary("Sends the operator to GitHub to authorize the App.")
            .WithDescription(
                "Issues a single-use state value, keeps it in a short-lived cookie, and redirects. The state is "
                + "what proves the callback belongs to a sign-in this Console Ops started.")
            .ExcludeFromDescription();

        return auth;
    }

    private static IResult Handle(
        HttpContext context,
        IGitHubUserAuthentication authentication,
        IConfiguration configuration)
    {
        if (string.IsNullOrWhiteSpace(configuration["GitHub:App:ClientId"]))
        {
            return Results.Problem(
                statusCode: StatusCodes.Status503ServiceUnavailable,
                title: "Unavailable",
                detail: "GitHub sign-in is not configured on this Console Ops.",
                extensions: new Dictionary<string, object?> { ["code"] = "Auth.NotConfigured" });
        }

        // Enough entropy that a callback cannot be guessed, and kept only in an HttpOnly cookie.
        string state = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
        SessionCookie.WriteState(context.Response, state);

        Uri authorize = authentication.BuildAuthorizationUrl(
            state,
            AuthenticationEndpoints.CallbackUrl(context.Request, configuration));

        return Results.Redirect(authorize.ToString());
    }
}

internal static class SignInCallbackEndpoint
{
    public static RouteGroupBuilder MapSignInCallbackEndpoint(this RouteGroupBuilder auth)
    {
        auth.MapGet("/github/callback", Handle)
            .WithName("CompleteGitHubSignIn")
            .WithSummary("Completes the authorization and starts a session.")
            .WithDescription(
                "Verifies the state, exchanges the code, and records the session only for a GitHub account on the "
                + "operator allow list. Ends in a redirect rather than a payload, because a browser arrives here.")
            .ExcludeFromDescription();

        return auth;
    }

    private static async Task<IResult> Handle(
        HttpContext context,
        string? code,
        string? state,
        string? error,
        ISender sender,
        IConfiguration configuration,
        ILogger<Program> logger,
        CancellationToken cancellationToken)
    {
        try
        {
            return await CompleteAsync(context, code, state, error, sender, configuration, cancellationToken);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            // A browser is here, mid-redirect, having already authorized on GitHub. Letting this reach the global
            // handler leaves the operator looking at a problem document in the address bar with no way back, which
            // is exactly what a missing database table did. The fault is still logged in full; the operator gets
            // the sign-in screen and a reason it knows how to explain.
            logger.LogError(exception, "Completing GitHub sign-in failed unexpectedly.");
            return Failed(configuration, "unavailable");
        }
    }

    private static async Task<IResult> CompleteAsync(
        HttpContext context,
        string? code,
        string? state,
        string? error,
        ISender sender,
        IConfiguration configuration,
        CancellationToken cancellationToken)
    {
        string expected = context.Request.Cookies[SessionCookie.StateName] ?? string.Empty;
        SessionCookie.ClearState(context.Response);

        // Compared in fixed time, and a single use: the cookie is cleared above whether this succeeds or not.
        if (string.IsNullOrWhiteSpace(state)
            || string.IsNullOrWhiteSpace(expected)
            || !CryptographicOperations.FixedTimeEquals(
                System.Text.Encoding.UTF8.GetBytes(state),
                System.Text.Encoding.UTF8.GetBytes(expected)))
        {
            return Failed(configuration, "state");
        }

        if (!string.IsNullOrWhiteSpace(error) || string.IsNullOrWhiteSpace(code))
        {
            // The operator declined on GitHub, or GitHub declined. Either way there is nothing to exchange.
            return Failed(configuration, "declined");
        }

        Result<SignedInOperatorResponse> result = await sender.Send(
            new SignInWithGitHubCommand(code, AuthenticationEndpoints.CallbackUrl(context.Request, configuration)),
            cancellationToken);

        if (!result.IsSuccess)
        {
            return Failed(configuration, result.Error.Code);
        }

        SessionCookie.Write(context.Response, result.Value.SessionId, DateTimeOffset.UtcNow.AddDays(30));
        return Results.Redirect(SignedInDestination(configuration));
    }

    /// <summary>
    /// Sends the browser back to the app with a reason it can show.
    /// </summary>
    /// <remarks>
    /// A reason code rather than a message: the screen owns the wording, and a redirect parameter is somewhere an
    /// arbitrary string should never be reflected from.
    /// </remarks>
    private static IResult Failed(IConfiguration configuration, string reason) =>
        Results.Redirect($"{SignInDestination(configuration)}?error={Uri.EscapeDataString(reason)}");

    private static string SignedInDestination(IConfiguration configuration) =>
        configuration["Auth:SignedInRedirect"] ?? "/overview";

    private static string SignInDestination(IConfiguration configuration) =>
        configuration["Auth:SignInRedirect"] ?? "/sign-in";
}

internal static class GetSessionEndpoint
{
    public static RouteGroupBuilder MapGetSessionEndpoint(this RouteGroupBuilder auth)
    {
        auth.MapGet("/session", Handle)
            .WithName("GetOperatorSession")
            .WithSummary("Reports the signed-in operator.")
            .WithDescription(
                "Answers 403 when nobody is signed in, which is how a screen decides to show the sign-in page. "
                + "Renews the GitHub token when it is close to expiring, so a tab left open does not lose its "
                + "session while being watched.")
            .Produces<OperatorSessionResponse>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status403Forbidden);

        return auth;
    }

    private static async Task<IResult> Handle(
        HttpContext context,
        ISender sender,
        CancellationToken cancellationToken)
    {
        Result<OperatorSessionResponse> result = await sender.Send(
            new GetOperatorSessionQuery(SessionCookie.Read(context.Request)),
            cancellationToken);

        if (!result.IsSuccess)
        {
            // The cookie is cleared on the way out: a session the server no longer has should not keep being sent.
            SessionCookie.Clear(context.Response);
        }

        return result.ToHttpResult();
    }
}

internal static class SignOutEndpoint
{
    public static RouteGroupBuilder MapSignOutEndpoint(this RouteGroupBuilder auth)
    {
        auth.MapPost("/sign-out", Handle)
            .WithName("SignOutOperator")
            .WithSummary("Ends the current session.")
            .WithDescription(
                "Deletes the stored session, which is what makes a copied cookie stop working, and clears the "
                + "cookie. Answers 204 whether or not a session was there.")
            .Produces(StatusCodes.Status204NoContent);

        return auth;
    }

    private static async Task<IResult> Handle(
        HttpContext context,
        ISender sender,
        CancellationToken cancellationToken)
    {
        await sender.Send(new SignOutCommand(SessionCookie.Read(context.Request)), cancellationToken);
        SessionCookie.Clear(context.Response);
        return Results.NoContent();
    }
}
