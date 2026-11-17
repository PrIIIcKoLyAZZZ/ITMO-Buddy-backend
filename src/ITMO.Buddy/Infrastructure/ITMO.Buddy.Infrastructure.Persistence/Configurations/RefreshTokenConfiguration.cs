using ITMO.Buddy.Domain.Users;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ITMO.Buddy.Infrastructure.Persistence.Configurations;

public sealed class RefreshTokenConfiguration
    : IEntityTypeConfiguration<RefreshToken>
{
    public void Configure(
        EntityTypeBuilder<RefreshToken> builder)
    {
        builder.ToTable("refresh_tokens", table =>
        {
            table.HasCheckConstraint(
                "ck_refresh_tokens_hash_not_blank",
                "length(btrim(token_hash)) > 0");

            table.HasCheckConstraint(
                "ck_refresh_tokens_expiration",
                "expires_at_utc > created_at_utc");

            table.HasCheckConstraint(
                "ck_refresh_tokens_revocation",
                "revoked_at_utc IS NULL OR revoked_at_utc >= created_at_utc");
        });

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id)
            .HasColumnName("id")
            .ValueGeneratedNever();

        builder.Property(x => x.UserId)
            .HasColumnName("user_id")
            .IsRequired();

        builder.Property(x => x.TokenHash)
            .HasColumnName("token_hash")
            .IsRequired();

        builder.Property(x => x.CreatedAtUtc)
            .HasColumnName("created_at_utc")
            .HasColumnType("timestamp with time zone")
            .IsRequired();

        builder.Property(x => x.ExpiresAtUtc)
            .HasColumnName("expires_at_utc")
            .HasColumnType("timestamp with time zone")
            .IsRequired();

        builder.Property(x => x.RevokedAtUtc)
            .HasColumnName("revoked_at_utc")
            .HasColumnType("timestamp with time zone");

        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(x => x.UserId)
            .OnDelete(DeleteBehavior.Cascade)
            .HasConstraintName(
                "fk_refresh_tokens_users_user_id");

        builder.HasIndex(x => x.TokenHash)
            .IsUnique()
            .HasDatabaseName(
                "ux_refresh_tokens_token_hash");

        builder.HasIndex(x => x.UserId)
            .HasDatabaseName(
                "ix_refresh_tokens_user_id");

        builder.HasIndex(x => x.ExpiresAtUtc)
            .HasDatabaseName(
                "ix_refresh_tokens_expires_at_utc");

        builder.Ignore(x => x.IsRevoked);
    }
}