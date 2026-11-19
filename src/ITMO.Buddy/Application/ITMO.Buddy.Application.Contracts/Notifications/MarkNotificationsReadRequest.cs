namespace ITMO.Buddy.Application.Contracts.Notifications;

/// <summary>
/// Запрос отметки уведомлений прочитанными.
/// Поддерживает выборочную и массовую отметку.
/// </summary>
public sealed record MarkNotificationsReadRequest
{
    /// <summary>
    /// Идентификаторы уведомлений для отметки.
    /// Используется при MarkAll = false.
    /// </summary>
    public IReadOnlyList<Guid>? NotificationIds
    {
        get;
        init;
    }

    /// <summary>
    /// Отметить все уведомления текущего
    /// пользователя прочитанными.
    /// </summary>
    public bool MarkAll { get; init; }
}