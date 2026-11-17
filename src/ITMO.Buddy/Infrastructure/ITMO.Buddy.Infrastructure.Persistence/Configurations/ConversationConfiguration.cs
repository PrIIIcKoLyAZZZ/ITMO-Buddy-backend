using ITMO.Buddy.Domain.Conversations;
using ITMO.Buddy.Domain.Users;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ITMO.Buddy.Infrastructure.Persistence.Configurations;

public sealed class ConversationConfiguration
    : IEntityTypeConfiguration<Conversation>
{
    public void Configure(
        EntityTypeBuilder<Conversation> builder)
    {
        builder.ToTable("conversations", table =>
        {
            table.HasCheckConstraint(
                "ck_conversations_different_users",
                "first_user_id <> second_user_id");

            table.HasCheckConstraint(
                "ck_conversations_source_type",
                "source_type IN ('Match', 'ListingResponse')");
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

        builder.Property(x => x.SourceType)
            .HasColumnName("source_type")
            .HasConversion<string>()
            .HasMaxLength(32)
            .IsRequired();

        builder.Property(x => x.SourceId)
            .HasColumnName("source_id")
            .IsRequired();

        builder.Property(x => x.CreatedAtUtc)
            .HasColumnName("created_at_utc")
            .HasColumnType("timestamp with time zone")
            .IsRequired();

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
                "fk_conversations_first_user");

        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(x => x.SecondUserId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName(
                "fk_conversations_second_user");

        builder.HasIndex(
                "PairMinUserId",
                "PairMaxUserId")
            .IsUnique()
            .HasDatabaseName(
                "ux_conversations_unordered_pair");

        builder.HasIndex(x => new
            {
                x.FirstUserId,
                x.CreatedAtUtc
            })
            .HasDatabaseName(
                "ix_conversations_first_user_created_at");

        builder.HasIndex(x => new
            {
                x.SecondUserId,
                x.CreatedAtUtc
            })
            .HasDatabaseName(
                "ix_conversations_second_user_created_at");

        builder.HasIndex(x => new
            {
                x.SourceType,
                x.SourceId
            })
            .HasDatabaseName(
                "ix_conversations_source");
    }
}