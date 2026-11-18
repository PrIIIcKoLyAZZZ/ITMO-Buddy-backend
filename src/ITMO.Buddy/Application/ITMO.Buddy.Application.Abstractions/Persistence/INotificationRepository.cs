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

    Task<int> CountOwnedAsync(
        Guid recipientId, IReadOnlyCollection<Guid> notificationIds, CancellationToken cancellationToken);

    /// <summary>Marks selected notifications and their incoming messages in the caller's transaction.</summary>
    Task<int> MarkReadAsync(
        Guid recipientId, IReadOnlyCollection<Guid> notificationIds,
        DateTimeOffset readAtUtc, CancellationToken cancellationToken);

    /// <summary>Marks all notifications and their incoming messages in the caller's transaction.</summary>
    Task<int> MarkAllReadAsync(
        Guid recipientId, DateTimeOffset readAtUtc, CancellationToken cancellationToken);

    Task<int> MarkMessageNotificationsReadAsync(
        Guid recipientId, IReadOnlyCollection<Guid> messageIds,
        DateTimeOffset readAtUtc, CancellationToken cancellationToken);

    void Add(Notification notification);

    void Remove(Notification notification);
}