using ITMO.Buddy.Application.Contracts.Common;

namespace ITMO.Buddy.Application.Contracts.Notifications;

/// <summary>
/// Страница уведомлений с общим количеством
/// непрочитанных уведомлений пользователя.
/// </summary>
public sealed record NotificationsPageResponse(
    PagedResult<NotificationResponse> Notifications,
    int UnreadCount);