using ConsoleOps.Api.Security;

namespace ConsoleOps.Api.Extensions;

/// <summary>
/// Refuses to start an unauthenticated Console Ops on any address other than loopback.
/// <para>
/// Console Ops has no user accounts by design, which is safe only while it answers on loopback. Its endpoints
/// expose repository names and probe operator-supplied URLs, so bound anywhere else it must require a key.
/// Failing at startup is deliberate: the alternative is a service that looks fine and is open.
/// </para>
/// </summary>
public static class ExposureGuardExtensions
{
    public static WebApplication EnsureSafeExposure(this WebApplication app, IConfiguration configuration)
    {
        if (!NetworkExposure.MustRefuseToStart(BoundUrls(app, configuration), configuration["Api:Key"]))
        {
            return app;
        }

        throw new InvalidOperationException(
            "Console Ops is bound to a non-loopback address without 'Api:Key' configured. Set Api:Key "
            + "(user-secrets or environment) so requests must send the "
            + $"{ApiKeyAuthentication.HeaderName} header, or bind to localhost only.");
    }

    /// <summary>
    /// What the host will listen on, falling back to configuration when the server has not resolved its
    /// addresses yet.
    /// </summary>
    private static string[] BoundUrls(WebApplication app, IConfiguration configuration) =>
        app.Urls.Count > 0
            ? [.. app.Urls]
            : [configuration["urls"] ?? configuration["ASPNETCORE_URLS"] ?? "http://localhost"];
}
