using ITMO.Buddy.Application.Contracts.Common;
using ITMO.Buddy.Application.Contracts.Notifications;

namespace ITMO.Buddy.Application.Abstractions.Services;

/// <summary>
/// Сервис получения и обработки уведомлений.
/// </summary>
public interface INotificationService
{
    /// <summary>
    /// Возвращает страницу уведомлений
    /// текущего пользователя.
    /// </summary>
    Task<NotificationsPageResponse>
        GetMyNotificationsAsync(
            PaginationRequest pagination,
            CancellationToken cancellationToken = default);

    /// <summary>
    /// Возвращает количество непрочитанных
    /// уведомлений текущего пользователя.
    /// </summary>
    Task<int> GetUnreadCountAsync(
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Отмечает выбранные уведомления прочитанными.
    /// Все уведомления должны принадлежать
    /// текущему пользователю.
    /// </summary>
    Task MarkAsReadAsync(
        IReadOnlyCollection<Guid> notificationIds,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Отмечает все уведомления текущего
    /// пользователя прочитанными.
    /// </summary>
    Task MarkAllAsReadAsync(
        CancellationToken cancellationToken = default);
}