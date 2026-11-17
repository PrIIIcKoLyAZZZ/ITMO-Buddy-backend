using ITMO.Buddy.Domain.Common;
using ITMO.Buddy.Domain.Enums;

namespace ITMO.Buddy.Domain.Conversations;

public sealed class Conversation : Entity
{
    public Guid FirstUserId { get; private set; }

    public Guid SecondUserId { get; private set; }

    public ConversationSourceType SourceType { get; private set; }

    public Guid SourceId { get; private set; }

    public DateTimeOffset CreatedAtUtc { get; private set; }

    private Conversation()
    {
    }

    public Conversation(
        Guid id,
        Guid firstUserId,
        Guid secondUserId,
        ConversationSourceType sourceType,
        Guid sourceId,
        DateTimeOffset createdAtUtc)
        : base(id)
    {
        if (firstUserId == Guid.Empty ||
            secondUserId == Guid.Empty)
        {
            throw new DomainException(
                "conversation.user_id.empty",
                "User identifiers cannot be empty.");
        }

        if (firstUserId == secondUserId)
        {
            throw new DomainException(
                "conversation.same_user",
                "Conversation requires two different users.");
        }

        if (!Enum.IsDefined(sourceType))
        {
            throw new DomainException(
                "conversation.source_type.invalid",
                "Unknown conversation source type.");
        }

        if (sourceId == Guid.Empty)
        {
            throw new DomainException(
                "conversation.source_id.empty",
                "Conversation source identifier cannot be empty.");
        }

        if (firstUserId.CompareTo(secondUserId) < 0)
        {
            FirstUserId = firstUserId;
            SecondUserId = secondUserId;
        }
        else
        {
            FirstUserId = secondUserId;
            SecondUserId = firstUserId;
        }

        SourceType = sourceType;
        SourceId = sourceId;

        CreatedAtUtc = UtcTime.Require(
            createdAtUtc,
            nameof(createdAtUtc));
    }

    public bool ContainsUser(Guid userId)
    {
        return FirstUserId == userId ||
               SecondUserId == userId;
    }

    public bool IsBetween(Guid firstUserId, Guid secondUserId)
    {
        return firstUserId != secondUserId &&
               ContainsUser(firstUserId) &&
               ContainsUser(secondUserId);
    }

    public Guid GetOtherParticipantId(Guid userId)
    {
        if (!ContainsUser(userId))
        {
            throw new DomainException(
                "conversation.user.not_participant",
                "User is not a participant of this conversation.");
        }

        return FirstUserId == userId
            ? SecondUserId
            : FirstUserId;
    }

    public void EnsureParticipant(Guid userId)
    {
        if (!ContainsUser(userId))
        {
            throw new DomainException(
                "conversation.user.not_participant",
                "User is not a participant of this conversation.");
        }
    }
}