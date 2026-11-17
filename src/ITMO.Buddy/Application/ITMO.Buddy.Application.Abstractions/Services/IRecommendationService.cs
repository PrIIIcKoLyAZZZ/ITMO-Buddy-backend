using ITMO.Buddy.Application.Contracts.Common;
using ITMO.Buddy.Application.Contracts.Recommendations;

namespace ITMO.Buddy.Application.Abstractions.Services;

/// <summary>
/// Сервис персональных рекомендаций пользователей.
/// </summary>
public interface IRecommendationService
{
    /// <summary>
    /// Возвращает рекомендации с рассчитанной
    /// совместимостью и объяснениями совпадения.
    /// </summary>
    Task<PagedResult<RecommendationResponse>>
        GetRecommendationsAsync(
            GetRecommendationsRequest request,
            CancellationToken cancellationToken = default);

    /// <summary>
    /// Обрабатывает пропуск рекомендованного пользователя.
    /// </summary>
    Task SkipRecommendationAsync(
        Guid targetUserId,
        CancellationToken cancellationToken = default);
}