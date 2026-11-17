using ITMO.Buddy.Application.Contracts.Common;
using ITMO.Buddy.Application.Contracts.Responses;

namespace ITMO.Buddy.Application.Abstractions.Services;

/// <summary>
/// Сервис отправки и рассмотрения откликов
/// на объявления.
/// </summary>
public interface IListingResponseService
{
    /// <summary>
    /// Создаёт отклик текущего пользователя.
    /// </summary>
    Task<ListingResponseDto> RespondAsync(
        Guid listingId,
        CreateListingResponseRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Принимает отклик.
    /// Доступно только автору объявления.
    /// </summary>
    Task<ListingResponseDto> AcceptAsync(
        Guid responseId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Отклоняет отклик.
    /// Доступно только автору объявления.
    /// </summary>
    Task<ListingResponseDto> RejectAsync(
        Guid responseId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Возвращает отклики на объявление.
    /// Доступно автору объявления.
    /// </summary>
    Task<PagedResult<ListingResponseDto>>
        GetListingResponsesAsync(
            Guid listingId,
            PaginationRequest pagination,
            CancellationToken cancellationToken = default);

    /// <summary>
    /// Возвращает отклики текущего пользователя.
    /// </summary>
    Task<PagedResult<ListingResponseDto>>
        GetMyResponsesAsync(
            PaginationRequest pagination,
            CancellationToken cancellationToken = default);
}