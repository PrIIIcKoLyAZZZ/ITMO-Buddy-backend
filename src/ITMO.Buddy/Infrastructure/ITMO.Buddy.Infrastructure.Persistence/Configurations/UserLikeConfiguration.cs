using ITMO.Buddy.Domain.Interactions;
using ITMO.Buddy.Domain.Users;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ITMO.Buddy.Infrastructure.Persistence.Configurations;

public sealed class UserLikeConfiguration
    : IEntityTypeConfiguration<UserLike>
{
    public void Configure(
        EntityTypeBuilder<UserLike> builder)
    {
        builder.ToTable("user_likes", table =>
        {
            table.HasCheckConstraint(
                "ck_user_likes_different_users",
                "sender_id <> target_id");
        });

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id)
            .HasColumnName("id")
            .ValueGeneratedNever();

        builder.Property(x => x.SenderId)
            .HasColumnName("sender_id")
            .IsRequired();

        builder.Property(x => x.TargetId)
            .HasColumnName("target_id")
            .IsRequired();

        builder.Property(x => x.CreatedAtUtc)
            .HasColumnName("created_at_utc")
            .HasColumnType("timestamp with time zone")
            .IsRequired();

        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(x => x.SenderId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName(
                "fk_user_likes_sender");

        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(x => x.TargetId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName(
                "fk_user_likes_target");

        builder.HasIndex(x => new
            {
                x.SenderId,
                x.TargetId
            })
            .IsUnique()
            .HasDatabaseName(
                "ux_user_likes_sender_target");

        builder.HasIndex(x => new
            {
                x.TargetId,
                x.SenderId
            })
            .HasDatabaseName(
                "ix_user_likes_target_sender");

        builder.HasIndex(x => new
            {
                x.SenderId,
                x.CreatedAtUtc
            })
            .HasDatabaseName(
                "ix_user_likes_sender_created_at");
    }
}