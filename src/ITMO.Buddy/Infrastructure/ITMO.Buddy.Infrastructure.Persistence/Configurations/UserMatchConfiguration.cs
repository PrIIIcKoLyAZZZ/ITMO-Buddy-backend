using ITMO.Buddy.Domain.Interactions;
using ITMO.Buddy.Domain.Users;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ITMO.Buddy.Infrastructure.Persistence.Configurations;

public sealed class UserMatchConfiguration
    : IEntityTypeConfiguration<UserMatch>
{
    public void Configure(
        EntityTypeBuilder<UserMatch> builder)
    {
        builder.ToTable("user_matches", table =>
        {
            table.HasCheckConstraint(
                "ck_user_matches_different_users",
                "first_user_id <> second_user_id");
        });

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id)
            .HasColumnName("id")
            .ValueGeneratedNever();

        builder.Property(x => x.FirstUserId)
            .HasColumnName("first_user_id")
            .IsRequired();

        builder.Property(x => x.SecondUserId)
            .HasColumnName("second_user_id")
            .IsRequired();

        builder.Property(x => x.CreatedAtUtc)
            .HasColumnName("created_at_utc")
            .HasColumnType("timestamp with time zone")
            .IsRequired();

        // Database-level canonical ordering.
        // These properties exist only in EF Core.

        builder.Property<Guid>("PairMinUserId")
            .HasColumnName("pair_min_user_id")
            .HasComputedColumnSql(
                "LEAST(first_user_id, second_user_id)",
                stored: true);

        builder.Property<Guid>("PairMaxUserId")
            .HasColumnName("pair_max_user_id")
            .HasComputedColumnSql(
                "GREATEST(first_user_id, second_user_id)",
                stored: true);

        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(x => x.FirstUserId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName(
                "fk_user_matches_first_user");

        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(x => x.SecondUserId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName(
                "fk_user_matches_second_user");

        builder.HasIndex(
                "PairMinUserId",
                "PairMaxUserId")
            .IsUnique()
            .HasDatabaseName(
                "ux_user_matches_unordered_pair");

        builder.HasIndex(x => x.FirstUserId)
            .HasDatabaseName(
                "ix_user_matches_first_user_id");

        builder.HasIndex(x => x.SecondUserId)
            .HasDatabaseName(
                "ix_user_matches_second_user_id");
    }
}