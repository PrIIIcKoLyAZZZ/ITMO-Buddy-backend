namespace ITMO.Buddy.Application.Contracts.Listings;

/// <summary>
/// Облегчённая карточка объявления для ленты.
/// </summary>
public sealed record ListingCardResponse(
    Guid Id,
    ListingAuthorResponse Author,
    string Title,
    string DescriptionPreview,
    string Category,
    string Format,
    int RequiredParticipantsCount,
    int AcceptedParticipantsCount,
    DateTimeOffset? DeadlineUtc,
    DateTimeOffset? EventAtUtc,
    string Status,
    DateTimeOffset CreatedAtUtc);