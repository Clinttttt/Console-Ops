namespace ConsoleOps.Application.Integrations.Diagnostics;

/// <summary>
/// Reports which configuration Console Ops needs is present, by key name only.
/// <para>
/// Never a value, not even a redacted one. The point of this port is to answer "why is GitHub failing?"
/// without becoming a way to read secrets out of a running instance.
/// </para>
/// </summary>
public interface IConfigurationInspector
{
    IReadOnlyList<ConfigurationKeyStatus> Inspect();
}

/// <param name="Key">The configuration key, such as <c>GitHub:Token</c>. Names only.</param>
/// <param name="Capability">What this key unlocks, so the screen can report a capability rather than a key.</param>
/// <param name="IsRequired">
/// Whether Console Ops needs it in this deployment. Conditional: <c>Api:Key</c> is required only when bound
/// somewhere other than loopback.
/// </param>
public sealed record ConfigurationKeyStatus(
    string Key,
    string Capability,
    ConfigurationKeyState State,
    bool IsRequired);

/// <summary>
/// Whether a key was set. <see cref="Default"/> separates "left alone deliberately" from "forgotten": a
/// section with working defaults is not missing anything.
/// </summary>
public enum ConfigurationKeyState
{
    Configured,
    Missing,
    Default
}
