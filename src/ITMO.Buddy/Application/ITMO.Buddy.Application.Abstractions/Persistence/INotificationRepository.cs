using ITMO.Buddy.Domain.Notifications;

namespace ITMO.Buddy.Application.Abstractions.Persistence;

public interface INotificationRepository
{
    Task<Notification?> GetByIdAsync(
        Guid notificationId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Notification>> GetForRecipientAsync(
        Guid recipientId,
        int page,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Общее количество уведомлений пользователя.
    /// </summary>
    Task<int> CountForRecipientAsync(
        Guid recipientId,
        CancellationToken cancellationToken = default);

    Task<int> CountUnreadAsync(
        Guid recipientId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Notification>>
        GetUnreadForRecipientAsync(
            Guid recipientId,
            int take,
            CancellationToken cancellationToken = default);

    void Add(Notification notification);

    void Remove(Notification notification);
}