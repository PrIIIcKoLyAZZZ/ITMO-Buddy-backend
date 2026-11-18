using ITMO.Buddy.Domain.Conversations;

namespace ITMO.Buddy.Application.Abstractions.Persistence.Queries;

public sealed record ConversationParticipantItem(
    string? FirstName,
    string? LastName,
    string? PhotoObjectKey);

public sealed record ConversationListItem(
    Guid ConversationId,
    Guid OtherUserId,
    ConversationParticipantItem? OtherProfile,
    Message? LastMessage,
    int UnreadCount);

public interface IConversationQueries
{
    Task<QueryPage<ConversationListItem>> GetPageAsync(
        Guid viewerId,
        int page,
        CancellationToken cancellationToken);
}
