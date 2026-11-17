namespace ITMO.Buddy.Application.Abstractions.Recommendations;

/// <summary>
/// Абстракция временного исключения пользователей
/// из персональных рекомендаций.
/// </summary>
/// <remarks>
/// Не требует обязательного постоянного хранения
/// в PostgreSQL.
/// </remarks>
public interface IRecommendationSkipStore
{
    /// <summary>
    /// Сохраняет пропуск рекомендованного пользователя.
    /// Повторная операция должна быть идемпотентной.
    /// </summary>
    Task SkipAsync(
        Guid userId,
        Guid targetUserId,
        DateTimeOffset expiresAtUtc,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Возвращает идентификаторы пользователей,
    /// имеющих действующий пропуск, среди
    /// переданных кандидатов.
    /// </summary>
    Task<IReadOnlySet<Guid>> GetActiveSkippedIdsAsync(
        Guid userId,
        IReadOnlyCollection<Guid> candidateUserIds,
        DateTimeOffset nowUtc,
        CancellationToken cancellationToken = default);
}