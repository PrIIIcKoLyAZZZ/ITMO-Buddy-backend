using ITMO.Buddy.Domain.Conversations;

namespace ITMO.Buddy.Application.Abstractions.Persistence;

public interface IConversationRepository
{
    Task<Conversation?> GetByIdAsync(
        Guid conversationId,
        CancellationToken cancellationToken = default);

    Task<Conversation?> GetBetweenAsync(
        Guid firstUserId,
        Guid secondUserId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Conversation>> GetForUserAsync(
        Guid userId,
        int page,
        CancellationToken cancellationToken = default);

    void Add(Conversation conversation);

    void Remove(Conversation conversation);
}