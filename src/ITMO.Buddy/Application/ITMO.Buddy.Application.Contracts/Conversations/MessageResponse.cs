namespace ITMO.Buddy.Application.Contracts.Conversations;

/// <summary>
/// Публичная модель сообщения.
/// </summary>
public sealed record MessageResponse(
    Guid Id,
    Guid ConversationId,
    Guid SenderId,
    string Text,
    DateTimeOffset SentAtUtc,
    DateTimeOffset? ReadAtUtc);