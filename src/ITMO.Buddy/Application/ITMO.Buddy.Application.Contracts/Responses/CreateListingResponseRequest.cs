namespace ITMO.Buddy.Application.Contracts.Responses;

/// <summary>
/// Запрос отправки отклика на объявление.
/// </summary>
public sealed record CreateListingResponseRequest
{
    /// <summary>
    /// Необязательное сопроводительное сообщение.
    /// Максимальная длина — 500 символов.
    /// </summary>
    public string? Message { get; init; }
}