using ConsoleOps.Infrastructure.Persistence.Authentication;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ConsoleOps.Infrastructure.Persistence.Configurations;

internal sealed class OperatorSessionConfiguration : IEntityTypeConfiguration<OperatorSessionEntity>
{
    /// <summary>A protected token is longer than the token it protects, so the column is sized generously.</summary>
    private const int ProtectedTokenMaxLength = 4_000;

    public void Configure(EntityTypeBuilder<OperatorSessionEntity> builder)
    {
        builder.ToTable("operator_sessions");

        builder.HasKey(session => session.Id).HasName("pk_operator_sessions");

        builder.Property(session => session.Id)
            .HasColumnName("id")
            .ValueGeneratedNever();

        builder.Property(session => session.GitHubUserId)
            .HasColumnName("github_user_id")
            .IsRequired();

        builder.Property(session => session.Login)
            .HasColumnName("login")
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(session => session.AvatarUrl)
            .HasColumnName("avatar_url")
            .HasMaxLength(2_048);

        builder.Property(session => session.ProtectedAccessToken)
            .HasColumnName("protected_access_token")
            .HasMaxLength(ProtectedTokenMaxLength)
            .IsRequired();

        builder.Property(session => session.AccessTokenExpiresAtUtc)
            .HasColumnName("access_token_expires_at_utc")
            .IsRequired();

        builder.Property(session => session.ProtectedRefreshToken)
            .HasColumnName("protected_refresh_token")
            .HasMaxLength(ProtectedTokenMaxLength);

        builder.Property(session => session.RefreshTokenExpiresAtUtc)
            .HasColumnName("refresh_token_expires_at_utc");

        builder.Property(session => session.SignedInAtUtc)
            .HasColumnName("signed_in_at_utc")
            .IsRequired();

        builder.Property(session => session.LastSeenAtUtc)
            .HasColumnName("last_seen_at_utc")
            .IsRequired();

        // Sessions are pruned by expiry, and an operator's own sessions are worth finding by login.
        builder.HasIndex(session => session.RefreshTokenExpiresAtUtc)
            .HasDatabaseName("ix_operator_sessions_refresh_expiry");

        builder.HasIndex(session => session.Login)
            .HasDatabaseName("ix_operator_sessions_login");
    }
}
