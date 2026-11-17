using ITMO.Buddy.Domain.Interactions;
using ITMO.Buddy.Domain.Users;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ITMO.Buddy.Infrastructure.Persistence.Configurations;

public sealed class UserBlockConfiguration
    : IEntityTypeConfiguration<UserBlock>
{
    public void Configure(
        EntityTypeBuilder<UserBlock> builder)
    {
        builder.ToTable("user_blocks", table =>
        {
            table.HasCheckConstraint(
                "ck_user_blocks_different_users",
                "blocker_id <> blocked_user_id");
        });

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id)
            .HasColumnName("id")
            .ValueGeneratedNever();

        builder.Property(x => x.BlockerId)
            .HasColumnName("blocker_id")
            .IsRequired();

        builder.Property(x => x.BlockedUserId)
            .HasColumnName("blocked_user_id")
            .IsRequired();

        builder.Property(x => x.CreatedAtUtc)
            .HasColumnName("created_at_utc")
            .HasColumnType("timestamp with time zone")
            .IsRequired();

        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(x => x.BlockerId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName(
                "fk_user_blocks_blocker");

        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(x => x.BlockedUserId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName(
                "fk_user_blocks_blocked_user");

        builder.HasIndex(x => new
            {
                x.BlockerId,
                x.BlockedUserId
            })
            .IsUnique()
            .HasDatabaseName(
                "ux_user_blocks_blocker_blocked");

        builder.HasIndex(x => new
            {
                x.BlockedUserId,
                x.BlockerId
            })
            .HasDatabaseName(
                "ix_user_blocks_blocked_blocker");

        builder.HasIndex(x => new
            {
                x.BlockerId,
                x.CreatedAtUtc
            })
            .HasDatabaseName(
                "ix_user_blocks_blocker_created_at");
    }
}