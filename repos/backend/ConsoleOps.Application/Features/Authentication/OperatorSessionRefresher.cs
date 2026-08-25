using ConsoleOps.Application.Integrations.GitHub;

namespace ConsoleOps.Application.Features.Authentication;

/// <summary>
/// Keeps a session's GitHub token usable.
/// </summary>
/// <remarks>
/// <para>
/// One place, because two callers need it: the session read a screen makes, and the credential the reading adapters
/// use on the operator's behalf. Two copies would eventually disagree about when a session is over, which is the one
/// judgement here that must not be made twice.
/// </para>
/// <para>
/// The distinction it exists to hold: GitHub being unreachable is not an operator being signed out. Only GitHub
/// rejecting the refresh token ends a session, because reporting somebody as signed in while every read they make
/// fails is worse than asking them to sign in again.
/// </para>
/// </remarks>
public sealed class OperatorSessionRefresher(
    IOperatorSessionStore sessions,
    IGitHubUserAuthentication authentication,
    TimeProvider timeProvider)
{
    /// <summary>Refreshed this far ahead of expiry, so a read in flight does not fail on a token that just died.</summary>
    public static readonly TimeSpan RefreshWindow = TimeSpan.FromMinutes(10);

    /// <summary>
    /// The session with a usable token, or <c>null</c> when it cannot be kept alive.
    /// </summary>
    /// <remarks>
    /// A session that cannot be renewed is deleted rather than returned, so a caller never gets a token it already
    /// knows will be refused.
    /// </remarks>
    public async Task<OperatorSession?> EnsureFreshAsync(
        OperatorSession session,
        CancellationToken cancellationToken)
    {
        DateTimeOffset now = timeProvider.GetUtcNow();
        if (session.AccessTokenExpiresAtUtc - now > RefreshWindow)
        {
            return session;
        }

        if (session.RefreshToken is null
            || (session.RefreshTokenExpiresAtUtc is { } refreshExpiry && refreshExpiry <= now))
        {
            await sessions.DeleteAsync(session.Id, cancellationToken);
            return null;
        }

        GitHubAuthenticationResult<GitHubUserToken> refreshed = await authentication.RefreshAsync(
            session.RefreshToken,
            cancellationToken);

        if (!refreshed.IsSuccess)
        {
            if (refreshed.Failure == GitHubAuthenticationFailure.Rejected)
            {
                await sessions.DeleteAsync(session.Id, cancellationToken);
                return null;
            }

            // Unreachable, not refused: the session survives a transient failure with the token it already has.
            return session;
        }

        GitHubUserToken token = refreshed.Value!;
        OperatorSession updated = session with
        {
            AccessToken = token.AccessToken,
            AccessTokenExpiresAtUtc = token.AccessTokenExpiresAtUtc,
            RefreshToken = token.RefreshToken ?? session.RefreshToken,
            RefreshTokenExpiresAtUtc = token.RefreshTokenExpiresAtUtc ?? session.RefreshTokenExpiresAtUtc,
            LastSeenAtUtc = now,
        };

        await sessions.SaveAsync(updated, cancellationToken);
        return updated;
    }
}
