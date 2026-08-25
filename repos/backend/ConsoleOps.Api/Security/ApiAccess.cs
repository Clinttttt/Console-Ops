using Microsoft.AspNetCore.Http;

namespace ConsoleOps.Api.Security;

/// <summary>
/// Which requests must be authenticated, and which cannot be.
/// </summary>
/// <remarks>
/// <para>
/// Kept as a policy rather than left inside the middleware so it can be asserted directly. Getting this wrong in
/// either direction is expensive: too broad locks an operator out of the sign-in that would let them in, too narrow
/// leaves the product surface open.
/// </para>
/// <para>
/// The sign-in paths are always reachable. <c>/api/auth/session</c> in particular is how a screen discovers that
/// nobody is signed in - answering it with a demand to sign in first would be circular.
/// </para>
/// </remarks>
public static class ApiAccess
{
    private const string ProtectedPrefix = "/api";
    private const string SignInPrefix = "/api/auth";

    public static bool IsSignInPath(PathString path) =>
        path.StartsWithSegments(SignInPrefix, StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Whether this request has to prove itself.
    /// </summary>
    /// <remarks>
    /// Only when something is configured to prove it against. A Console Ops with neither sign-in nor a key behaves
    /// as it always has - open on loopback, refused at startup anywhere else - so local development is unchanged by
    /// the arrival of sign-in.
    /// </remarks>
    public static bool RequiresAuthentication(
        bool signInConfigured,
        string? apiKey,
        PathString path) =>
        (signInConfigured || !string.IsNullOrWhiteSpace(apiKey))
        && path.StartsWithSegments(ProtectedPrefix, StringComparison.OrdinalIgnoreCase)
        && !IsSignInPath(path);
}
