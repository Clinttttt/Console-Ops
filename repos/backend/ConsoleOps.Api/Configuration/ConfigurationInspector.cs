using ConsoleOps.Api.Security;
using ConsoleOps.Application.Integrations.Diagnostics;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;

namespace ConsoleOps.Api.Configuration;

/// <summary>
/// Reports which configuration Console Ops needs is present.
/// <para>
/// It lives in the API layer because two of its answers depend on the host: whether a key is required at all
/// depends on the addresses being listened on, and configuration itself is a boundary concern.
/// </para>
/// <para>
/// Values are never read. Every method here asks only whether a key has a non-blank value, so no code path
/// exists that could return, log, or compare a secret.
/// </para>
/// </summary>
public sealed class ConfigurationInspector(IConfiguration configuration, IServer server)
    : IConfigurationInspector
{
    private const string Database = "Database";
    private const string SourceAndCi = "Source and CI";
    private const string Azure = "Azure";
    private const string Exposure = "Exposure";
    private const string Collection = "Collection";
    private const string SignIn = "Sign-in";

    public IReadOnlyList<ConfigurationKeyStatus> Inspect()
    {
        // Sign-in is optional, but configuring it makes three other keys mandatory. Reporting them as required only
        // once the App is configured keeps a local Console Ops from reading as misconfigured.
        bool signInStarted = !string.IsNullOrWhiteSpace(configuration["GitHub:App:ClientId"]);

        return
        [
            Required("ConnectionStrings:DefaultConnection", Database),
            Required("GitHub:Token", SourceAndCi),

            // A key is only required once Console Ops answers somewhere other than loopback. Reusing the startup
            // guard's own rule means the report and the guard cannot drift apart.
            new ConfigurationKeyStatus(
                "Api:Key",
                Exposure,
                State("Api:Key"),
                !NetworkExposure.IsLoopbackOnly(BoundAddresses())),

            new ConfigurationKeyStatus("GitHub:App:ClientId", SignIn, State("GitHub:App:ClientId"), false),
            new ConfigurationKeyStatus(
                "GitHub:App:ClientSecret",
                SignIn,
                State("GitHub:App:ClientSecret"),
                signInStarted),

            // Empty admits nobody, so a configured sign-in with no operators is a console no one can use.
            new ConfigurationKeyStatus(
                "Auth:AllowedGitHubLogins",
                SignIn,
                State("Auth:AllowedGitHubLogins"),
                signInStarted),

            // Without it the keys protecting stored tokens live in the container's own filesystem, so a restart
            // loses every session and a second replica cannot read what the first one wrote.
            new ConfigurationKeyStatus(
                "DataProtection:BlobUri",
                SignIn,
                State("DataProtection:BlobUri"),
                signInStarted),

            // Unset means DefaultAzureCredential falls back to the ambient identity, which is how a developer
            // signed in with az login is expected to run. Absent is a choice here, not a fault.
            Optional("Azure:TenantId", Azure),
            Optional("Azure:ClientId", Azure),
            Optional("Azure:ClientSecret", Azure),
            Optional("Azure:Monitor", Azure),

            Optional("Monitoring:Refresh", Collection),
        ];
    }

    private ConfigurationKeyStatus Required(string key, string capability) =>
        new(key, capability, State(key), true);

    private ConfigurationKeyStatus Optional(string key, string capability) =>
        new(key, capability, State(key), false);

    /// <summary>
    /// Whether the key has a value, or whether a section has any values under it. Blank counts as unset, since
    /// an empty string configures nothing.
    /// </summary>
    private ConfigurationKeyState State(string key)
    {
        if (!string.IsNullOrWhiteSpace(configuration[key]))
        {
            return ConfigurationKeyState.Configured;
        }

        IConfigurationSection section = configuration.GetSection(key);
        return section.Exists() && section.GetChildren().Any()
            ? ConfigurationKeyState.Configured
            : ConfigurationKeyState.Missing;
    }

    /// <summary>
    /// What the host is actually listening on, falling back to configuration when the server has not resolved
    /// its addresses.
    /// </summary>
    private string[] BoundAddresses()
    {
        ICollection<string>? addresses = server.Features.Get<IServerAddressesFeature>()?.Addresses;

        return addresses is { Count: > 0 }
            ? [.. addresses]
            : [configuration["urls"] ?? configuration["ASPNETCORE_URLS"] ?? "http://localhost"];
    }
}
