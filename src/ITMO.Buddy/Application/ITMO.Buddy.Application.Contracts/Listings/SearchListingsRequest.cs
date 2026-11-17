namespace ITMO.Buddy.Application.Contracts.Listings;

/// <summary>
/// Параметры поиска и фильтрации объявлений.
/// </summary>
public sealed record SearchListingsRequest
{
    public string? SearchText { get; init; }

    /// <summary>
    /// Необязательная категория объявления.
    /// </summary>
    public string? Category { get; init; }

    /// <summary>
    /// Фильтрация по необходимым навыкам.
    /// OR внутри коллекции.
    /// </summary>
    public IReadOnlyList<Guid>? SkillIds
    {
        get;
        init;
    }

    /// <summary>
    /// Offline или Online.
    /// </summary>
    public string? Format { get; init; }

    public int Page { get; init; } = 1;

    public int PageSize { get; init; } = 20;
}