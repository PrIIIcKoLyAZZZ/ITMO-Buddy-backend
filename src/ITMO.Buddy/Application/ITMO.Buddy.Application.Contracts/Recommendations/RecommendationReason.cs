namespace ITMO.Buddy.Application.Contracts.Recommendations;

/// <summary>
/// Объяснение одной из причин рекомендации.
/// </summary>
/// <param name="Code">
/// Стабильный код причины.
/// </param>
/// <param name="Description">
/// Готовое человекочитаемое объяснение.
/// </param>
public sealed record RecommendationReason(
    string Code,
    string Description);