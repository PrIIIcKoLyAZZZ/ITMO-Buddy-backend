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

    /// <summary>
    /// Возвращает количество диалогов пользователя.
    /// </summary>
    Task<int> CountForUserAsync(
        Guid userId,
        CancellationToken cancellationToken = default);

    /// <summary>Creates a conversation if the unordered pair has none, within the caller's transaction.</summary>
    Task EnsureBetweenAsync(Conversation conversation, CancellationToken cancellationToken);

    void Add(Conversation conversation);

    void Remove(Conversation conversation);
}