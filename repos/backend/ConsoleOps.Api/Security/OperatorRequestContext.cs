using ConsoleOps.Application.Features.Authentication;

namespace ConsoleOps.Api.Security;

/// <summary>
/// The operator this request belongs to, once authentication has established one.
/// </summary>
/// <remarks>
/// Set by <see cref="ApiAuthenticationMiddleware"/> after the session passed every check, so anything downstream can
/// act as that operator without repeating the lookup or trusting a cookie a second time. Absent for a request
/// authenticated by the API key, which has no operator by definition.
/// </remarks>
public static class OperatorRequestContext
{
    private const string SessionKey = "ConsoleOps.OperatorSession";

    public static void Set(HttpContext context, OperatorSession session) =>
        context.Items[SessionKey] = session;

    public static OperatorSession? Get(HttpContext context) =>
        context.Items.TryGetValue(SessionKey, out object? value) ? value as OperatorSession : null;
}
