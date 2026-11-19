using ITMO.Buddy.Domain.Common;

namespace ITMO.Buddy.Domain.Conversations;

public sealed class Message : Entity
{
    public const int MaxTextLength = 2000;

    public Guid ConversationId { get; private set; }

    public Guid SenderId { get; private set; }

    public string Text { get; private set; } = null!;

    public DateTimeOffset SentAtUtc { get; private set; }

    public DateTimeOffset? ReadAtUtc { get; private set; }

    public bool IsRead => ReadAtUtc.HasValue;

    private Message()
    {
    }

    public Message(
        Guid id,
        Conversation conversation,
        Guid senderId,
        string text,
        DateTimeOffset sentAtUtc)
        : base(id)
    {
        if (conversation is null)
        {
            throw new DomainException(
                "message.conversation.null",
                "Conversation cannot be null.");
        }

        conversation.EnsureParticipant(senderId);

        var normalizedText = text?.Trim();

        if (string.IsNullOrWhiteSpace(normalizedText))
        {
            throw new DomainException(
                "message.text.empty",
                "Message text cannot be empty.");
        }

        if (normalizedText.Length > MaxTextLength)
        {
            throw new DomainException(
                "message.text.too_long",
                $"Message text cannot exceed {MaxTextLength} characters.");
        }

        var timestamp = UtcTime.Require(
            sentAtUtc,
            nameof(sentAtUtc));

        if (timestamp < conversation.CreatedAtUtc)
        {
            throw new DomainException(
                "message.sent_at.invalid",
                "Message cannot be sent before conversation creation.");
        }

        ConversationId = conversation.Id;
        SenderId = senderId;
        Text = normalizedText;
        SentAtUtc = timestamp;
    }

    public bool IsUnreadFor(
        Guid userId,
        Conversation conversation)
    {
        EnsureConversation(conversation);

        conversation.EnsureParticipant(userId);

        return userId != SenderId && !IsRead;
    }

    public void MarkAsRead(
        Conversation conversation,
        Guid readerId,
        DateTimeOffset readAtUtc)
    {
        EnsureConversation(conversation);

        conversation.EnsureParticipant(readerId);

        if (readerId == SenderId)
        {
            throw new DomainException(
                "message.read.by_sender",
                "Sender cannot mark their own message as read.");
        }

        var timestamp = UtcTime.Require(
            readAtUtc,
            nameof(readAtUtc));

        if (IsRead)
        {
            return;
        }

        if (timestamp < SentAtUtc)
        {
            throw new DomainException(
                "message.read.invalid_time",
                "Message cannot be read before it was sent.");
        }

        ReadAtUtc = timestamp;
    }

    private void EnsureConversation(Conversation conversation)
    {
        if (conversation is null ||
            conversation.Id != ConversationId)
        {
            throw new DomainException(
                "message.conversation.mismatch",
                "Message does not belong to this conversation.");
        }
    }
}