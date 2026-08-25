namespace ConsoleOps.Application.Features.Authentication;

/// <summary>
/// One signed-in operator, as Console Ops holds them.
/// </summary>
/// <remarks>
/// The tokens are here because the API acts on the operator's behalf when it reads GitHub. They never leave the
/// server: the browser is given an opaque session id and nothing else, which is the same rule every other provider
/// credential in Console Ops follows.
/// </remarks>
public sealed record OperatorSession(
    Guid Id,
    long GitHubUserId,
    string Login,
    string? AvatarUrl,
    string AccessToken,
    DateTimeOffset AccessTokenExpiresAtUtc,
    string? RefreshToken,
    DateTimeOffset? RefreshTokenExpiresAtUtc,
    DateTimeOffset SignedInAtUtc,
    DateTimeOffset LastSeenAtUtc);

/// <summary>
/// Where signed-in operators are kept.
/// </summary>
/// <remarks>
/// Stored rather than held in memory so a restart or a new revision does not sign an operator out. The tokens are
/// encrypted at rest by the implementation; this port deals in plain values because the application layer is what
/// decides when a token is used, not how it is protected.
/// </remarks>
public interface IOperatorSessionStore
{
    Task SaveAsync(OperatorSession session, CancellationToken cancellationToken);

    Task<OperatorSession?> FindAsync(Guid sessionId, CancellationToken cancellationToken);

    /// <summary>Records that a session was used, so an idle one can be told from a live one.</summary>
    Task TouchAsync(Guid sessionId, DateTimeOffset seenAtUtc, CancellationToken cancellationToken);

    Task DeleteAsync(Guid sessionId, CancellationToken cancellationToken);

    /// <summary>Removes sessions whose refresh token has expired, so the table does not grow forever.</summary>
    Task<int> DeleteExpiredAsync(DateTimeOffset asOfUtc, CancellationToken cancellationToken);
}
