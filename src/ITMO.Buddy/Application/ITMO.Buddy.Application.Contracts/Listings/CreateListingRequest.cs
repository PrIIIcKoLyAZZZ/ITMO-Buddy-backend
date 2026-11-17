namespace ITMO.Buddy.Application.Contracts.Listings;

/// <summary>
/// Запрос создания объявления.
/// Автор определяется из контекста аутентификации.
/// </summary>
public sealed record CreateListingRequest
{
    public required string Title { get; init; }

    public required string Description { get; init; }

    /// <summary>
    /// Studies, Projects, Events, Interests.
    /// </summary>
    public required string Category { get; init; }

    public required IReadOnlyList<Guid> RequiredSkillIds
    {
        get;
        init;
    }

    public required int RequiredParticipantsCount
    {
        get;
        init;
    }

    /// <summary>
    /// Offline или Online.
    /// </summary>
    public required string Format { get; init; }

    public required DateTimeOffset? DeadlineUtc
    {
        get;
        init;
    }

    public required DateTimeOffset? EventAtUtc
    {
        get;
        init;
    }
}