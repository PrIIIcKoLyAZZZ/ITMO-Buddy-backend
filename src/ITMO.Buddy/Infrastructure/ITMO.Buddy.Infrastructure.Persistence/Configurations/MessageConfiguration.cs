using ITMO.Buddy.Domain.Conversations;
using ITMO.Buddy.Domain.Users;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ITMO.Buddy.Infrastructure.Persistence.Configurations;

public sealed class MessageConfiguration
    : IEntityTypeConfiguration<Message>
{
    public void Configure(
        EntityTypeBuilder<Message> builder)
    {
        builder.ToTable("messages", table =>
        {
            table.HasCheckConstraint(
                "ck_messages_text_not_blank",
                "length(btrim(text)) > 0");

            table.HasCheckConstraint(
                "ck_messages_read_date",
                "read_at_utc IS NULL OR " +
                "read_at_utc >= sent_at_utc");
        });

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id)
            .HasColumnName("id")
            .ValueGeneratedNever();

        builder.Property(x => x.ConversationId)
            .HasColumnName("conversation_id")
            .IsRequired();

        builder.Property(x => x.SenderId)
            .HasColumnName("sender_id")
            .IsRequired();

        builder.Property(x => x.Text)
            .HasColumnName("text")
            .HasMaxLength(Message.MaxTextLength)
            .IsRequired();

        builder.Property(x => x.SentAtUtc)
            .HasColumnName("sent_at_utc")
            .HasColumnType("timestamp with time zone")
            .IsRequired();

        builder.Property(x => x.ReadAtUtc)
            .HasColumnName("read_at_utc")
            .HasColumnType("timestamp with time zone");

        builder.Ignore(x => x.IsRead);

        builder.HasOne<Conversation>()
            .WithMany()
            .HasForeignKey(x => x.ConversationId)
            .OnDelete(DeleteBehavior.Cascade)
            .HasConstraintName(
                "fk_messages_conversations");

        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(x => x.SenderId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName(
                "fk_messages_users_sender");

        builder.HasIndex(x => new
            {
                x.ConversationId,
                x.SentAtUtc,
                x.Id
            })
            .HasDatabaseName(
                "ix_messages_conversation_sent_id");

        builder.HasIndex(x => new
            {
                x.ConversationId,
                x.SenderId
            })
            .HasFilter("read_at_utc IS NULL")
            .HasDatabaseName(
                "ix_messages_unread_conversation_sender");

        builder.HasIndex(x => x.SenderId)
            .HasDatabaseName(
                "ix_messages_sender_id");
    }
}