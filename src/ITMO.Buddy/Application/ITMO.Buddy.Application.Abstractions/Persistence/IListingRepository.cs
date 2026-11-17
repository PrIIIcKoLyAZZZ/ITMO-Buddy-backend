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

    Task<IReadOnlyList<ListingResponse>> GetCandidateResponsesAsync(
        Guid candidateId,
        int page,
        CancellationToken cancellationToken = default);

    void Add(Listing listing);

    void Remove(Listing listing);
}