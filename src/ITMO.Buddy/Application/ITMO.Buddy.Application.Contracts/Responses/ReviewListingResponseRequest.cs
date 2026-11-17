namespace ITMO.Buddy.Application.Contracts.Responses;

/// <summary>
/// Решение автора объявления по отклику.
/// Используется с PATCH /api/responses/{id}.
/// </summary>
public sealed record ReviewListingResponseRequest
{
    /// <summary>
    /// Допустимые значения: Accept, Reject.
    /// </summary>
    public required string Decision { get; init; }
}