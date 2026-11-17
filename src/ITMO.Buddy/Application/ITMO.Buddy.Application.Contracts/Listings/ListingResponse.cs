using ITMO.Buddy.Application.Contracts.Dictionaries;

namespace ITMO.Buddy.Application.Contracts.Listings;

/// <summary>
/// Полная публичная модель объявления.
/// </summary>
public sealed record ListingResponse(
    Guid Id,
    ListingAuthorResponse Author,
    string Title,
    string Description,
    string Category,
    IReadOnlyList<DictionaryItemResponse> RequiredSkills,
    int RequiredParticipantsCount,
    int AcceptedParticipantsCount,
    string Format,
    DateTimeOffset? DeadlineUtc,
    DateTimeOffset? EventAtUtc,
    string Status,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset UpdatedAtUtc);