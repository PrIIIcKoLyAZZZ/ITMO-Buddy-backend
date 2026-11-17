using ITMO.Buddy.Application.Abstractions.Persistence;
using ITMO.Buddy.Domain.Listings;
using ITMO.Buddy.Infrastructure.Persistence.Context;
using Microsoft.EntityFrameworkCore;

namespace ITMO.Buddy.Infrastructure.Persistence.Repositories;

public sealed class ListingRepository : IListingRepository
{
    private readonly BuddyDbContext _context;

    public ListingRepository(BuddyDbContext context)
    {
        _context = context;
    }

    public Task<Listing?> GetByIdAsync(
        Guid listingId,
        CancellationToken cancellationToken = default)
    {
        return CreateAggregateQuery()
            .SingleOrDefaultAsync(
                x => x.Id == listingId,
                cancellationToken);
    }

    public Task<Listing?> GetByResponseIdAsync(
        Guid responseId,
        CancellationToken cancellationToken = default)
    {
        return CreateAggregateQuery()
            .SingleOrDefaultAsync(
                x => x.Responses.Any(
                    response => response.Id == responseId),
                cancellationToken);
    }

    public async Task<IReadOnlyList<ListingResponse>>
        GetListingResponsesAsync(
            Guid listingId,
            int page,
            CancellationToken cancellationToken = default)
    {
        var skip = RepositoryPaging.GetSkip(page);

        return await _context.ListingResponses
            .AsNoTracking()
            .Where(x => x.ListingId == listingId)
            .OrderByDescending(x => x.CreatedAtUtc)
            .ThenByDescending(x => x.Id)
            .Skip(skip)
            .Take(RepositoryPaging.PageSize)
            .ToListAsync(cancellationToken);
    }

    public Task<int> CountListingResponsesAsync(
        Guid listingId,
        CancellationToken cancellationToken = default)
    {
        return _context.ListingResponses
            .AsNoTracking()
            .CountAsync(
                x => x.ListingId == listingId,
                cancellationToken);
    }

    public async Task<IReadOnlyList<ListingResponse>>
        GetCandidateResponsesAsync(
            Guid candidateId,
            int page,
            CancellationToken cancellationToken = default)
    {
        var skip = RepositoryPaging.GetSkip(page);

        return await _context.ListingResponses
            .AsNoTracking()
            .Where(x => x.CandidateId == candidateId)
            .OrderByDescending(x => x.CreatedAtUtc)
            .ThenByDescending(x => x.Id)
            .Skip(skip)
            .Take(RepositoryPaging.PageSize)
            .ToListAsync(cancellationToken);
    }

    public Task<int> CountCandidateResponsesAsync(
        Guid candidateId,
        CancellationToken cancellationToken = default)
    {
        return _context.ListingResponses
            .AsNoTracking()
            .CountAsync(
                x => x.CandidateId == candidateId,
                cancellationToken);
    }

    public void Add(Listing listing)
    {
        ArgumentNullException.ThrowIfNull(listing);

        foreach (var skill in listing.RequiredSkills)
        {
            if (_context.Entry(skill).State ==
                EntityState.Detached)
            {
                throw new InvalidOperationException(
                    "Required skills must be tracked " +
                    "by the current DbContext.");
            }
        }

        _context.Listings.Add(listing);
    }

    public void Remove(Listing listing)
    {
        ArgumentNullException.ThrowIfNull(listing);

        _context.Listings.Remove(listing);
    }

    private IQueryable<Listing> CreateAggregateQuery()
    {
        return _context.Listings
            .Include(x => x.RequiredSkills)
            .Include(x => x.Responses)
            .AsSplitQuery();
    }
}