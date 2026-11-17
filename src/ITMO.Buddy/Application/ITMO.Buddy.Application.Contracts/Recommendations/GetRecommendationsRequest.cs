namespace ITMO.Buddy.Application.Contracts.Recommendations;

/// <summary>
/// Параметры получения персональных рекомендаций.
/// </summary>
public sealed record GetRecommendationsRequest
{
    /// <summary>
    /// Необязательная категория цели.
    /// Допустимые значения:
    /// Studies, Projects, Events, Interests.
    /// </summary>
    public string? GoalCategory { get; init; }

    public int Page { get; init; } = 1;

    public int PageSize { get; init; } = 20;
}