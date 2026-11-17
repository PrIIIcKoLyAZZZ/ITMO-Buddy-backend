using ITMO.Buddy.Domain.Notifications;
using ITMO.Buddy.Domain.Users;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ITMO.Buddy.Infrastructure.Persistence.Configurations;

public sealed class NotificationConfiguration
    : IEntityTypeConfiguration<Notification>
{
    public void Configure(
        EntityTypeBuilder<Notification> builder)
    {
        builder.ToTable("notifications", table =>
        {
            table.HasCheckConstraint(
                "ck_notifications_type",
                "type IN (" +
                "'MatchCreated', " +
                "'ListingResponseCreated', " +
                "'ListingResponseAccepted', " +
                "'ListingResponseRejected', " +
                "'MessageReceived')");

            table.HasCheckConstraint(
                "ck_notifications_read_date",
                "read_at_utc IS NULL OR " +
                "read_at_utc >= created_at_utc");
        });

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id)
            .HasColumnName("id")
            .ValueGeneratedNever();

        builder.Property(x => x.RecipientId)
            .HasColumnName("recipient_id")
            .IsRequired();

        builder.Property(x => x.Type)
            .HasColumnName("type")
            .HasConversion<string>()
            .HasMaxLength(64)
            .IsRequired();

        builder.Property(x => x.RelatedEntityId)
            .HasColumnName("related_entity_id")
            .IsRequired();

        builder.Property(x => x.CreatedAtUtc)
            .HasColumnName("created_at_utc")
            .HasColumnType("timestamp with time zone")
            .IsRequired();

        builder.Property(x => x.ReadAtUtc)
            .HasColumnName("read_at_utc")
            .HasColumnType("timestamp with time zone");

        builder.Ignore(x => x.IsRead);
        builder.Ignore(x => x.IsUnread);

        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(x => x.RecipientId)
            .OnDelete(DeleteBehavior.Cascade)
            .HasConstraintName(
                "fk_notifications_users_recipient");

        builder.HasIndex(x => new
            {
                x.RecipientId,
                x.CreatedAtUtc,
                x.Id
            })
            .HasDatabaseName(
                "ix_notifications_recipient_created_id");

        builder.HasIndex(x => x.RecipientId)
            .HasFilter("read_at_utc IS NULL")
            .HasDatabaseName(
                "ix_notifications_unread_recipient");
    }
}