using ConsoleOps.Application.Features.Authentication;
using ConsoleOps.Infrastructure.Persistence.Authentication;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;

namespace ConsoleOps.Infrastructure.Persistence.Repositories;

/// <summary>
/// Keeps signed-in operators, with their GitHub tokens encrypted at rest.
/// </summary>
/// <remarks>
/// <para>
/// Stored rather than held in memory so a restart or a new revision does not sign an operator out. That means the
/// tokens sit in a database, so they are protected before they get there: a database backup, a query result, or a
/// support session should not hand somebody a working GitHub token.
/// </para>
/// <para>
/// Protection is keyed by purpose. Data Protection keys must outlive the process for this to survive a deployment -
/// on Azure Container Apps the filesystem is ephemeral, so the keys are persisted externally and the deployment
/// guide says so. Without that, every revision invalidates every session and nobody stays signed in.
/// </para>
/// </remarks>
internal sealed class OperatorSessionStore : IOperatorSessionStore
{
    /// <summary>Names what the protected values are for, so they cannot be unprotected by another purpose.</summary>
    private const string ProtectionPurpose = "ConsoleOps.OperatorSession.GitHubToken.v1";

    private readonly ConsoleOpsDbContext dbContext;
    private readonly IDataProtector protector;

    public OperatorSessionStore(ConsoleOpsDbContext dbContext, IDataProtectionProvider protection)
    {
        this.dbContext = dbContext;
        protector = protection.CreateProtector(ProtectionPurpose);
    }

    public async Task SaveAsync(OperatorSession session, CancellationToken cancellationToken)
    {
        OperatorSessionEntity? existing = await dbContext.OperatorSessions
            .FirstOrDefaultAsync(entity => entity.Id == session.Id, cancellationToken);

        if (existing is null)
        {
            dbContext.OperatorSessions.Add(ToEntity(session));
        }
        else
        {
            existing.GitHubUserId = session.GitHubUserId;
            existing.Login = session.Login;
            existing.AvatarUrl = session.AvatarUrl;
            existing.ProtectedAccessToken = protector.Protect(session.AccessToken);
            existing.AccessTokenExpiresAtUtc = session.AccessTokenExpiresAtUtc;
            existing.ProtectedRefreshToken = session.RefreshToken is null
                ? null
                : protector.Protect(session.RefreshToken);
            existing.RefreshTokenExpiresAtUtc = session.RefreshTokenExpiresAtUtc;
            existing.LastSeenAtUtc = session.LastSeenAtUtc;
        }

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<OperatorSession?> FindAsync(Guid sessionId, CancellationToken cancellationToken)
    {
        OperatorSessionEntity? entity = await dbContext.OperatorSessions
            .AsNoTracking()
            .FirstOrDefaultAsync(session => session.Id == sessionId, cancellationToken);

        return entity is null ? null : ToSession(entity);
    }

    public async Task TouchAsync(
        Guid sessionId,
        DateTimeOffset seenAtUtc,
        CancellationToken cancellationToken) =>
        await dbContext.OperatorSessions
            .Where(session => session.Id == sessionId)
            .ExecuteUpdateAsync(
                setters => setters.SetProperty(session => session.LastSeenAtUtc, seenAtUtc),
                cancellationToken);

    public async Task DeleteAsync(Guid sessionId, CancellationToken cancellationToken) =>
        await dbContext.OperatorSessions
            .Where(session => session.Id == sessionId)
            .ExecuteDeleteAsync(cancellationToken);

    public async Task<int> DeleteExpiredAsync(DateTimeOffset asOfUtc, CancellationToken cancellationToken) =>
        await dbContext.OperatorSessions
            .Where(session =>
                session.RefreshTokenExpiresAtUtc != null && session.RefreshTokenExpiresAtUtc <= asOfUtc)
            .ExecuteDeleteAsync(cancellationToken);

    private OperatorSessionEntity ToEntity(OperatorSession session) => new()
    {
        Id = session.Id,
        GitHubUserId = session.GitHubUserId,
        Login = session.Login,
        AvatarUrl = session.AvatarUrl,
        ProtectedAccessToken = protector.Protect(session.AccessToken),
        AccessTokenExpiresAtUtc = session.AccessTokenExpiresAtUtc,
        ProtectedRefreshToken = session.RefreshToken is null ? null : protector.Protect(session.RefreshToken),
        RefreshTokenExpiresAtUtc = session.RefreshTokenExpiresAtUtc,
        SignedInAtUtc = session.SignedInAtUtc,
        LastSeenAtUtc = session.LastSeenAtUtc,
    };

    /// <summary>
    /// The stored session with its tokens readable again, or <c>null</c> when they cannot be.
    /// </summary>
    /// <remarks>
    /// A value that will not unprotect is treated as no session rather than as an error: it means the keys changed,
    /// and the honest consequence is that the operator signs in again.
    /// </remarks>
    private OperatorSession? ToSession(OperatorSessionEntity entity)
    {
        try
        {
            return new OperatorSession(
                entity.Id,
                entity.GitHubUserId,
                entity.Login,
                entity.AvatarUrl,
                protector.Unprotect(entity.ProtectedAccessToken),
                entity.AccessTokenExpiresAtUtc,
                entity.ProtectedRefreshToken is null
                    ? null
                    : protector.Unprotect(entity.ProtectedRefreshToken),
                entity.RefreshTokenExpiresAtUtc,
                entity.SignedInAtUtc,
                entity.LastSeenAtUtc);
        }
        catch (System.Security.Cryptography.CryptographicException)
        {
            return null;
        }
    }
}
