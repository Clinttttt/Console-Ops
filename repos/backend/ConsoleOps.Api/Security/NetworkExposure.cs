using System.Net;
using System.Security.Cryptography;
using System.Text;

namespace ConsoleOps.Api.Security;

/// <summary>
/// Decides whether Console Ops may listen on the addresses it was given.
/// </summary>
/// <remarks>
/// Console Ops is a single-operator tool and deliberately has no user accounts. That is safe while it
/// answers only on loopback. The moment it is bound to an address other machines can reach, its
/// endpoints expose repository names and will probe operator-supplied URLs, so it must not start without
/// a configured key. This is a guard against accidental exposure, not an authorization system.
/// </remarks>
public static class NetworkExposure
{
    /// <summary>
    /// Whether Console Ops must refuse to start: reachable from another machine with nothing guarding it.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The decision lives here rather than in the startup wiring so it can be tested directly. A guard that
    /// only runs when a host boots is a guard nobody checks.
    /// </para>
    /// <para>
    /// Configured sign-in satisfies it as well as a key. Sign-in is the stronger of the two - it says who a caller
    /// is, and the allow list says whether they may be here - so requiring a shared key alongside it would only
    /// mean an exposed deployment needed a secret it no longer uses.
    /// </para>
    /// </remarks>
    public static bool MustRefuseToStart(
        IEnumerable<string> urls,
        string? apiKey,
        bool signInConfigured = false) =>
        !IsLoopbackOnly(urls) && string.IsNullOrWhiteSpace(apiKey) && !signInConfigured;

    /// <summary>Marker for "Kestrel's default addresses", which are loopback.</summary>
    public static bool IsLoopbackOnly(IEnumerable<string> urls)
    {
        ArgumentNullException.ThrowIfNull(urls);

        foreach (string url in urls.SelectMany(SplitUrls))
        {
            if (!IsLoopbackAddress(url))
            {
                return false;
            }
        }

        return true;
    }

    private static IEnumerable<string> SplitUrls(string value) =>
        (value ?? string.Empty)
            .Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

    private static bool IsLoopbackAddress(string url)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out Uri? uri))
        {
            // An address Console Ops cannot parse is treated as exposed: refusing is the safe default.
            return false;
        }

        string host = uri.Host;

        // Kestrel's wildcards accept traffic from any interface.
        if (host is "*" or "+" or "0.0.0.0" or "[::]" or "::")
        {
            return false;
        }

        if (string.Equals(host, "localhost", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return IPAddress.TryParse(host, out IPAddress? address) && IPAddress.IsLoopback(address);
    }
}

/// <summary>
/// Optional shared-key check for the API surface.
/// </summary>
/// <remarks>
/// Enforced only when a key is configured, so local development needs no header. It is a single shared
/// secret guarding one operator's own API, not an identity system: it says "this caller knows the key",
/// nothing about who they are.
/// </remarks>
public static class ApiKeyAuthentication
{
    public const string HeaderName = "X-Console-Ops-Key";

    /// <summary>Paths under this prefix require the key when one is configured.</summary>
    private const string ProtectedPrefix = "/api";

    public static bool RequiresKey(string? configuredKey, PathString path) =>
        !string.IsNullOrWhiteSpace(configuredKey)
        && path.StartsWithSegments(ProtectedPrefix, StringComparison.OrdinalIgnoreCase);

    /// <summary>Compared in fixed time so a wrong key cannot be narrowed down by timing.</summary>
    public static bool IsKeyValid(string? configuredKey, string? presentedKey)
    {
        if (string.IsNullOrWhiteSpace(configuredKey) || string.IsNullOrEmpty(presentedKey))
        {
            return false;
        }

        byte[] expected = Encoding.UTF8.GetBytes(configuredKey.Trim());
        byte[] presented = Encoding.UTF8.GetBytes(presentedKey);

        return CryptographicOperations.FixedTimeEquals(expected, presented);
    }
}
