namespace ITMO.Buddy.Application.Contracts.Notifications;

/// <summary>
/// Публичная модель уведомления пользователя.
/// </summary>
public sealed record NotificationResponse(
    Guid Id,
    string Type,
    Guid RelatedEntityId,
    DateTimeOffset CreatedAtUtc,
    bool IsRead);