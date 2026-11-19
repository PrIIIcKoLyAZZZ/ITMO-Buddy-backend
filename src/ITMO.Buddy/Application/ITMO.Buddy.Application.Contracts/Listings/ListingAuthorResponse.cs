namespace ITMO.Buddy.Application.Contracts.Listings;

/// <summary>
/// Минимальные публичные сведения об авторе объявления.
/// </summary>
public sealed record ListingAuthorResponse(
    Guid UserId,
    string? FirstName,
    string? LastName,
    string? PhotoUrl);