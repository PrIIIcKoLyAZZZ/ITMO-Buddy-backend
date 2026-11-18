using ITMO.Buddy.Domain.Listings;

namespace ITMO.Buddy.Application.Abstractions.Persistence;

public interface IListingRepository
{
    Task<Listing?> GetByIdAsync(
        Guid listingId,
        CancellationToken cancellationToken = default);

    Task<Listing?> GetDetailsAsync(Guid listingId, CancellationToken cancellationToken);

    Task<Listing?> GetForEditingAsync(Guid listingId, CancellationToken cancellationToken);

    Task<Listing?> GetForRespondingAsync(Guid listingId, Guid candidateId, CancellationToken cancellationToken);

    Task<Guid?> GetAuthorIdAsync(Guid listingId, CancellationToken cancellationToken);

    Task<Listing?> GetByResponseIdAsync(
        Guid responseId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Получает отклики активных кандидатов без блокировки с автором объявления.
    /// </summary>
    Task<IReadOnlyList<ListingResponse>>
        GetListingResponsesAsync(
            Guid listingId,
            int page,
            CancellationToken cancellationToken = default);

    /// <summary>
    /// Общее количество откликов объявления, доступных автору.
    /// </summary>
    Task<int> CountListingResponsesAsync(
        Guid listingId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ListingResponse>>
        GetCandidateResponsesAsync(
            Guid candidateId,
            int page,
            CancellationToken cancellationToken = default);

    /// <summary>
    /// Общее количество откликов пользователя.
    /// </summary>
    Task<int> CountCandidateResponsesAsync(
        Guid candidateId,
        CancellationToken cancellationToken = default);

    void AddResponse(ListingResponse response);

    /// <summary>Checks the aggregate version at save even if its timestamp is unchanged.</summary>
    void RequireCurrentVersion(Listing listing);

    Task RemoveResponseNotificationsAsync(Guid listingId, CancellationToken cancellationToken);

    void Add(Listing listing);

    void Remove(Listing listing);
}