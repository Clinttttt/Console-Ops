namespace ConsoleOps.Application.Integrations.GitHub;

/// <summary>
/// Which GitHub credential the current work should use.
/// </summary>
/// <remarks>
/// <para>
/// Console Ops holds two, deliberately. A request an operator made is served with that operator's own token, so
/// GitHub applies their access and attributes what they start to them. Scheduled collection has no operator, so it
/// keeps the configured service token.
/// </para>
/// <para>
/// A port rather than a constructor argument because the choice is made per call, not per adapter: the reading
/// adapters must not know that sign-in exists, and nothing here decides what a token may do - GitHub does.
/// </para>
/// </remarks>
public interface IGitHubCredential
{
    /// <summary>
    /// The token to send, or <c>null</c> when Console Ops has none.
    /// </summary>
    /// <remarks>
    /// Null is a real answer: an unauthenticated read of a public repository still works, and letting GitHub refuse
    /// the request keeps one place - the adapter's own error mapping - deciding what a refusal means.
    /// </remarks>
    ValueTask<string?> GetTokenAsync(CancellationToken cancellationToken);
}
