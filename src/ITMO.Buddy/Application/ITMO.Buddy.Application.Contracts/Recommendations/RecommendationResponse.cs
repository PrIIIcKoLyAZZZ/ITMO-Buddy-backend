using ITMO.Buddy.Application.Contracts.Profiles;

namespace ITMO.Buddy.Application.Contracts.Recommendations;

/// <summary>
/// Персональная рекомендация пользователя.
/// </summary>
/// <param name="User">
/// Облегчённая карточка рекомендуемого пользователя.
/// </param>
/// <param name="Score">
/// Оценка совместимости от 0 до 1 включительно.
/// </param>
/// <param name="Reasons">
/// Причины рекомендации.
/// </param>
public sealed record RecommendationResponse(
    ProfileCardResponse User,
    double Score,
    IReadOnlyList<RecommendationReason> Reasons);