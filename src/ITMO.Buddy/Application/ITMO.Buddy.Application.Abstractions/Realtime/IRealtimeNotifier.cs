using ITMO.Buddy.Application.Contracts.Conversations;
using ITMO.Buddy.Application.Contracts.Likes;
using ITMO.Buddy.Application.Contracts.Responses;

namespace ITMO.Buddy.Application.Abstractions.Realtime;

/// <summary>
/// Абстракция доставки событий текущим
/// подключённым пользователям.
/// </summary>
/// <remarks>
/// Реализация через SignalR находится
/// за пределами Application Layer.
/// События отправляются после сохранения
/// соответствующих изменений.
/// </remarks>
public interface IRealtimeNotifier
{
    /// <summary>
    /// Уведомляет получателя о новом сообщении.
    /// </summary>
    Task NotifyMessageReceivedAsync(
        Guid recipientUserId,
        MessageResponse message,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Уведомляет пользователя о новом мэтче.
    /// </summary>
    Task NotifyMatchCreatedAsync(
        Guid recipientUserId,
        MatchResponse match,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Уведомляет автора объявления
    /// о новом отклике.
    /// </summary>
    Task NotifyListingResponseCreatedAsync(
        Guid recipientUserId,
        ListingResponseDto response,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Уведомляет кандидата о принятии
    /// или отклонении его отклика.
    /// </summary>
    Task NotifyListingResponseReviewedAsync(
        Guid recipientUserId,
        ListingResponseDto response,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Передаёт обновлённое количество
    /// непрочитанных уведомлений.
    /// </summary>
    Task NotifyUnreadCountChangedAsync(
        Guid recipientUserId,
        int unreadCount,
        CancellationToken cancellationToken = default);
}