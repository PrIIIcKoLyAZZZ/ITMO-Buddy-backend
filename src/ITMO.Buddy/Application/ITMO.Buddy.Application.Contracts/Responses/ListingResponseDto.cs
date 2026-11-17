using ITMO.Buddy.Application.Contracts.Profiles;

namespace ITMO.Buddy.Application.Contracts.Responses;

/// <summary>
/// Публичная модель отклика на объявление.
/// </summary>
public sealed record ListingResponseDto(
    Guid Id,
    Guid ListingId,
    ProfileCardResponse Candidate,
    string? Message,
    string Status,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset? DecidedAtUtc);