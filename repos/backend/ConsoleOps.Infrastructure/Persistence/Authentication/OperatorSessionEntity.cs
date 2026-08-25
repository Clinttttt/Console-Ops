namespace ConsoleOps.Infrastructure.Persistence.Authentication;

/// <summary>
/// A signed-in operator as it is stored.
/// </summary>
/// <remarks>
/// The token columns hold protected values, not tokens. They are named for what they are so nobody reading a schema
/// or a query result mistakes them for something usable.
/// </remarks>
internal sealed class OperatorSessionEntity
{
    public Guid Id { get; set; }

    public long GitHubUserId { get; set; }

    public string Login { get; set; } = string.Empty;

    public string? AvatarUrl { get; set; }

    public string ProtectedAccessToken { get; set; } = string.Empty;

    public DateTimeOffset AccessTokenExpiresAtUtc { get; set; }

    public string? ProtectedRefreshToken { get; set; }

    public DateTimeOffset? RefreshTokenExpiresAtUtc { get; set; }

    public DateTimeOffset SignedInAtUtc { get; set; }

    public DateTimeOffset LastSeenAtUtc { get; set; }
}
