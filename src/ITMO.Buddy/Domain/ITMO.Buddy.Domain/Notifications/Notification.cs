using ITMO.Buddy.Domain.Common;
using ITMO.Buddy.Domain.Enums;

namespace ITMO.Buddy.Domain.Notifications;

public sealed class Notification : Entity
{
    public Guid RecipientId { get; private set; }

    public NotificationType Type { get; private set; }

    public Guid RelatedEntityId { get; private set; }

    public DateTimeOffset CreatedAtUtc { get; private set; }

    public DateTimeOffset? ReadAtUtc { get; private set; }

    public bool IsRead => ReadAtUtc.HasValue;

    public bool IsUnread => !IsRead;

    private Notification()
    {
    }

    public Notification(
        Guid id,
        Guid recipientId,
        NotificationType type,
        Guid relatedEntityId,
        DateTimeOffset createdAtUtc)
        : base(id)
    {
        if (recipientId == Guid.Empty)
        {
            throw new DomainException(
                "notification.recipient_id.empty",
                "Recipient identifier cannot be empty.");
        }

        if (!Enum.IsDefined(type))
        {
            throw new DomainException(
                "notification.type.invalid",
                "Unknown notification type.");
        }

        if (relatedEntityId == Guid.Empty)
        {
            throw new DomainException(
                "notification.related_entity_id.empty",
                "Related entity identifier cannot be empty.");
        }

        RecipientId = recipientId;
        Type = type;
        RelatedEntityId = relatedEntityId;

        CreatedAtUtc = UtcTime.Require(
            createdAtUtc,
            nameof(createdAtUtc));
    }

    public bool IsFor(Guid userId)
    {
        return RecipientId == userId;
    }

    public void MarkAsRead(DateTimeOffset readAtUtc)
    {
        var timestamp = UtcTime.Require(
            readAtUtc,
            nameof(readAtUtc));

        if (IsRead)
        {
            return;
        }

        if (timestamp < CreatedAtUtc)
        {
            throw new DomainException(
                "notification.read.invalid_time",
                "Notification cannot be read before creation.");
        }

        ReadAtUtc = timestamp;
    }
}