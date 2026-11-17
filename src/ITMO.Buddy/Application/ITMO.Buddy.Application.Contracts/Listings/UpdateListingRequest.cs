namespace ITMO.Buddy.Application.Contracts.Listings;

/// <summary>
/// Полная замена редактируемых полей объявления.
/// Используется с PUT /api/listings/{id}.
/// </summary>
public sealed record UpdateListingRequest
{
    public required string Title { get; init; }

    public required string Description { get; init; }

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