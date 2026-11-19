namespace ITMO.Buddy.Application.Contracts.Conversations;

/// <summary>
/// Краткие публичные сведения о собеседнике.
/// </summary>
public sealed record ConversationParticipantResponse(
    Guid UserId,
    string? FirstName,
    string? LastName,
    string? PhotoUrl);