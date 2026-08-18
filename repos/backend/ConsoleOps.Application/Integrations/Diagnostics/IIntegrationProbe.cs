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

/// <summary>
/// What the last probe of each integration established, for this process.
/// <para>
/// A verification belongs to Console Ops, not to a browser tab: it is a fact about whether this process can
/// reach a provider. Keeping it here means every tab and every reload sees the same answer, and the cheap read
/// can report "verified at 17:41" instead of forgetting that anything was ever checked.
/// </para>
/// <para>
/// In memory, like the collection journal: it describes this process and is empty after a restart, which is
/// honest - a new process has not verified anything.
/// </para>
/// </summary>
public interface IProbeJournal
{
    ProbeOutcome? Last(string capability);

    void Record(string capability, ProbeOutcome outcome);
}

/// <param name="CheckedAt">
/// When the check ran. Reported so a verification is never mistaken for a current one: the screen says when it
/// was established rather than implying it is happening now.
/// </param>
public sealed record ProbeOutcome(bool Succeeded, string? Failure, DateTimeOffset CheckedAt);

/// <param name="Failure">
/// Why the attempt failed, in the operator's terms and never containing a credential. <c>null</c> on success.
/// </param>
public sealed record IntegrationProbeResult(bool Succeeded, string? Failure)
{
    public static IntegrationProbeResult Success() => new(true, null);

    public static IntegrationProbeResult Failed(string failure) => new(false, failure);
}
