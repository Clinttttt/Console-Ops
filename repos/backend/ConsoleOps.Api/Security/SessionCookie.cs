using Microsoft.AspNetCore.Http;

namespace ConsoleOps.Api.Security;

/// <summary>
/// How a session reaches the server and back.
/// </summary>
/// <remarks>
/// <para>
/// The browser is given an opaque session id in a cookie and nothing else. It is <c>HttpOnly</c> so script cannot
/// read it, <c>Secure</c> so it does not travel in clear, and <c>SameSite=Lax</c> because the sign-in returns
/// through a redirect from GitHub - <c>Strict</c> would drop the cookie on exactly that hop.
/// </para>
/// <para>
/// Lax is sufficient here because the frontend and the API answer on one origin: the Vercel deployment rewrites
/// <c>/api</c> to the container app, so nothing about this is cross-site. A deployment that called the API
/// directly from another origin would need <c>None</c>, and would then have to defend against cross-site requests
/// some other way.
/// </para>
/// </remarks>
public static class SessionCookie
{
    public const string Name = "consoleops_session";

    /// <summary>The short-lived cookie that carries the anti-forgery state through the GitHub round trip.</summary>
    public const string StateName = "consoleops_signin_state";

    public static void Write(HttpResponse response, Guid sessionId, DateTimeOffset expiresAt) =>
        response.Cookies.Append(
            Name,
            sessionId.ToString("N"),
            new CookieOptions
            {
                HttpOnly = true,
                Secure = true,
                SameSite = SameSiteMode.Lax,
                Path = "/",
                Expires = expiresAt,
            });

    public static void Clear(HttpResponse response) =>
        response.Cookies.Append(
            Name,
            string.Empty,
            new CookieOptions
            {
                HttpOnly = true,
                Secure = true,
                SameSite = SameSiteMode.Lax,
                Path = "/",
                Expires = DateTimeOffset.UnixEpoch,
            });

    public static void WriteState(HttpResponse response, string state) =>
        response.Cookies.Append(
            StateName,
            state,
            new CookieOptions
            {
                HttpOnly = true,
                Secure = true,
                SameSite = SameSiteMode.Lax,
                Path = "/",
                // Long enough to authorize on GitHub, short enough that an abandoned attempt does not linger.
                Expires = DateTimeOffset.UtcNow.AddMinutes(10),
            });

    public static void ClearState(HttpResponse response) =>
        response.Cookies.Append(
            StateName,
            string.Empty,
            new CookieOptions
            {
                HttpOnly = true,
                Secure = true,
                SameSite = SameSiteMode.Lax,
                Path = "/",
                Expires = DateTimeOffset.UnixEpoch,
            });

    /// <summary>
    /// The session id the request carries, or <c>null</c> when it carries none.
    /// </summary>
    /// <remarks>
    /// A cookie that is not a session id is treated as no session rather than as an error: it is what an old or
    /// tampered cookie looks like, and neither deserves a different answer.
    /// </remarks>
    public static Guid? Read(HttpRequest request) =>
        Guid.TryParseExact(request.Cookies[Name], "N", out Guid sessionId) ? sessionId : null;
}
