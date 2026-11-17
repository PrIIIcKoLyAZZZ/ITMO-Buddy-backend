namespace ITMO.Buddy.Application.Contracts.Conversations;

/// <summary>
/// Карточка диалога для списка переписок.
/// </summary>
public sealed record ConversationResponse(
    Guid ConversationId,
    ConversationParticipantResponse OtherParticipant,
    MessageResponse? LastMessage,
    DateTimeOffset? LastMessageAtUtc,
    int UnreadCount);