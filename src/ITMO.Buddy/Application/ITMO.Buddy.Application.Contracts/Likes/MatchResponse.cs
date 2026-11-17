using ITMO.Buddy.Application.Contracts.Conversations;

namespace ITMO.Buddy.Application.Contracts.Likes;

/// <summary>
/// Результат создания или получения взаимного мэтча.
/// </summary>
public sealed record MatchResponse(
    Guid MatchId,
    ConversationParticipantResponse OtherUser,
    DateTimeOffset CreatedAtUtc,
    Guid? ConversationId);