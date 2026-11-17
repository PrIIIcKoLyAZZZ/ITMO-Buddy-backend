namespace ITMO.Buddy.Application.Contracts.Search;

/// <summary>
/// Параметры поиска пользователей.
/// Между группами фильтров используется AND.
/// Внутри каждой группы используется OR.
/// </summary>
public sealed record SearchUsersRequest
{
    /// <summary>
    /// Текст для поиска по имени, описанию,
    /// интересам и навыкам.
    /// Минимум 2 символа после нормализации.
    /// </summary>
    public string? SearchText { get; init; }

    /// <summary>
    /// Категории целей:
    /// Studies, Projects, Events, Interests.
    /// </summary>
    public IReadOnlyList<string>? GoalCategories
    {
        get;
        init;
    }

    public IReadOnlyList<Guid>? InterestIds
    {
        get;
        init;
    }

    public IReadOnlyList<Guid>? SkillIds
    {
        get;
        init;
    }

    public IReadOnlyList<Guid>? FacultyIds
    {
        get;
        init;
    }

    public IReadOnlyList<int>? Courses
    {
        get;
        init;
    }

    public int Page { get; init; } = 1;

    public int PageSize { get; init; } = 20;
}