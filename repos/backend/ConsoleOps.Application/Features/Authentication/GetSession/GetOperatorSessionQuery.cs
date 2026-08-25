using ConsoleOps.Application.Abstractions.Messaging;
using ConsoleOps.Application.Integrations.GitHub;
using MediatR;

namespace ConsoleOps.Application.Features.Authentication.GetSession;

/// <summary>
/// Who is signed in, if anybody.
/// </summary>
/// <remarks>
/// The screen asks this rather than being told at load: a session can expire while a tab is open, and a page that
/// assumed otherwise would show an operator a console it can no longer read.
/// </remarks>
public sealed record GetOperatorSessionQuery(Guid? SessionId) : IRequest<Result<OperatorSessionResponse>>;

/// <param name="ExpiresAt">
/// When the access token stops working. Present so a screen can say a session is ending rather than discover it
/// through a failed read.
/// </param>
public sealed record OperatorSessionResponse(
    string Login,
    string? AvatarUrl,
    DateTimeOffset SignedInAt,
    DateTimeOffset ExpiresAt);

/// <summary>
/// Reports the signed-in operator, refreshing the access token when it is close to expiring.
/// </summary>
/// <remarks>
/// A GitHub App's user token lasts hours, so an operator who leaves a tab open would otherwise be signed out
/// mid-session. Refreshing here - on the read every screen already makes - keeps that from happening without a
/// background job whose only purpose is to hold a token open.
/// </remarks>
public sealed class GetOperatorSessionQueryHandler(
    IOperatorSessionStore sessions,
    IGitHubUserAuthentication authentication,
    OperatorAllowList allowList,
    TimeProvider timeProvider)
    : IRequestHandler<GetOperatorSessionQuery, Result<OperatorSessionResponse>>
{
    /// <summary>Refreshed this far ahead of expiry, so a read in flight does not fail on a token that just died.</summary>
    internal static readonly TimeSpan RefreshWindow = TimeSpan.FromMinutes(10);

    public async Task<Result<OperatorSessionResponse>> Handle(
        GetOperatorSessionQuery request,
        CancellationToken cancellationToken)
    {
        if (request.SessionId is null)
        {
            return Result<OperatorSessionResponse>.Failure(AuthenticationErrors.NoSession);
        }

        OperatorSession? session = await sessions.FindAsync(request.SessionId.Value, cancellationToken);
        if (session is null)
        {
            return Result<OperatorSessionResponse>.Failure(AuthenticationErrors.NoSession);
        }

        // Re-checked on every read: an operator removed from the list should lose access without waiting for their
        // token to expire.
        if (!allowList.Admits(session.Login))
        {
            await sessions.DeleteAsync(session.Id, cancellationToken);
            return Result<OperatorSessionResponse>.Failure(AuthenticationErrors.NotAnOperator);
        }

        DateTimeOffset now = timeProvider.GetUtcNow();
        session = await KeepFreshAsync(session, now, cancellationToken);
        if (session is null)
        {
            return Result<OperatorSessionResponse>.Failure(AuthenticationErrors.NoSession);
        }

        await sessions.TouchAsync(session.Id, now, cancellationToken);

        return Result<OperatorSessionResponse>.Success(new OperatorSessionResponse(
            session.Login,
            session.AvatarUrl,
            session.SignedInAtUtc,
            session.AccessTokenExpiresAtUtc));
    }

    /// <summary>
    /// The session with a usable token, or <c>null</c> when it cannot be kept alive.
    /// </summary>
    /// <remarks>
    /// A session whose refresh fails is deleted rather than left in place: keeping a record whose token no longer
    /// works would report somebody as signed in while every read they make fails.
    /// </remarks>
    private async Task<OperatorSession?> KeepFreshAsync(
        OperatorSession session,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
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
            // A provider that could not be reached is not a signed-out operator, so the session survives a
            // transient failure and is only dropped when GitHub actually rejects the refresh token.
            if (refreshed.Failure == GitHubAuthenticationFailure.Rejected)
            {
                await sessions.DeleteAsync(session.Id, cancellationToken);
                return null;
            }

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
