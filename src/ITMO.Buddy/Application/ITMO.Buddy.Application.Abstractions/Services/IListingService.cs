using ITMO.Buddy.Application.Contracts.Common;
using ITMO.Buddy.Application.Contracts.Listings;

namespace ITMO.Buddy.Application.Abstractions.Services;

/// <summary>
/// Сервис создания, редактирования и поиска объявлений.
/// </summary>
public interface IListingService
{
    /// <summary>
    /// Создаёт объявление от имени текущего пользователя.
    /// </summary>
    Task<ListingResponse> CreateAsync(
        CreateListingRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Полностью заменяет редактируемые поля объявления.
    /// Доступно только автору.
    /// </summary>
    Task<ListingResponse> UpdateAsync(
        Guid listingId,
        UpdateListingRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Удаляет объявление.
    /// Доступно только автору.
    /// </summary>
    Task DeleteAsync(
        Guid listingId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Закрывает объявление для новых откликов.
    /// Доступно только автору.
    /// </summary>
    Task CloseAsync(
        Guid listingId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Возвращает доступное для просмотра объявление.
    /// </summary>
    Task<ListingResponse> GetByIdAsync(
        Guid listingId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Ищет публичные объявления.
    /// </summary>
    Task<PagedResult<ListingCardResponse>> SearchAsync(
        SearchListingsRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Возвращает объявления текущего пользователя.
    /// </summary>
    Task<PagedResult<ListingCardResponse>>
        GetMyListingsAsync(
            PaginationRequest pagination,
            CancellationToken cancellationToken = default);
}