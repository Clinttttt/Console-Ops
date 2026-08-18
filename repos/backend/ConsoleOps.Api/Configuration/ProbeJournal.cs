using System.Collections.Concurrent;
using ConsoleOps.Application.Integrations.Diagnostics;

namespace ConsoleOps.Api.Configuration;

/// <summary>
/// Remembers the last probe of each integration, for this process only.
/// </summary>
/// <remarks>
/// A singleton because a verification is a fact about this process rather than about a request or a browser
/// tab. Before this existed the result lived in the page's own memory, so a reload forgot that anything had
/// been checked and the screen fell back to reporting configuration alone.
/// </remarks>
public sealed class ProbeJournal : IProbeJournal
{
    private readonly ConcurrentDictionary<string, ProbeOutcome> _outcomes = new(StringComparer.Ordinal);

    public ProbeOutcome? Last(string capability) =>
        _outcomes.TryGetValue(capability, out ProbeOutcome? outcome) ? outcome : null;

    public void Record(string capability, ProbeOutcome outcome)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(capability);
        ArgumentNullException.ThrowIfNull(outcome);

        _outcomes[capability] = outcome;
    }
}
