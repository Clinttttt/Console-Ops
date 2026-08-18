namespace ConsoleOps.Application.Integrations.Diagnostics;

/// <summary>
/// Asks one integration whether the credentials it has actually work.
/// <para>
/// A configured key proves someone typed something, not that it is valid. A probe answers the question an
/// operator really has, and it costs a real round trip - so it runs only when asked for, never on the cheap
/// read that a screen loads with.
/// </para>
/// </summary>
public interface IIntegrationProbe
{
    /// <summary>Which capability this probe reports on, matching the capability names the inspector uses.</summary>
    string Capability { get; }

    Task<IntegrationProbeResult> ProbeAsync(CancellationToken cancellationToken);
}

/// <param name="Failure">
/// Why the attempt failed, in the operator's terms and never containing a credential. <c>null</c> on success.
/// </param>
public sealed record IntegrationProbeResult(bool Succeeded, string? Failure)
{
    public static IntegrationProbeResult Success() => new(true, null);

    public static IntegrationProbeResult Failed(string failure) => new(false, failure);
}
