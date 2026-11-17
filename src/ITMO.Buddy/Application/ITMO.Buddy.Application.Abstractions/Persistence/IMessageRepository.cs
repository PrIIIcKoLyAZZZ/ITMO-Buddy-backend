using ITMO.Buddy.Domain.Conversations;

namespace ITMO.Buddy.Application.Abstractions.Persistence;

public interface IMessageRepository
{
    Task<Message?> GetByIdAsync(
        Guid messageId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Message>> GetHistoryAsync(
        Guid conversationId,
        int page,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Общее количество сообщений в диалоге.
    /// </summary>
    Task<int> CountHistoryAsync(
        Guid conversationId,
        CancellationToken cancellationToken = default);

    Task<Message?> GetLatestAsync(
        Guid conversationId,
        CancellationToken cancellationToken = default);

    Task<int> CountUnreadAsync(
        Guid conversationId,
        Guid recipientId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Message>>
        GetUnreadForRecipientAsync(
            Guid conversationId,
            Guid recipientId,
            int take,
            CancellationToken cancellationToken = default);

    void Add(Message message);

    void Remove(Message message);
}