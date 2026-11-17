using ITMO.Buddy.Domain.Listings;

namespace ITMO.Buddy.Application.Abstractions.Persistence;

public interface IListingRepository
{
    Task<Listing?> GetByIdAsync(
        Guid listingId,
        CancellationToken cancellationToken = default);

    Task<Listing?> GetByResponseIdAsync(
        Guid responseId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Получает отклики конкретного объявления.
    /// </summary>
    Task<IReadOnlyList<ListingResponse>>
        GetListingResponsesAsync(
            Guid listingId,
            int page,
            CancellationToken cancellationToken = default);

    /// <summary>
    /// Общее количество откликов объявления.
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

    void Add(Listing listing);

    void Remove(Listing listing);
}