using ITMO.Buddy.Application.Abstractions.Persistence;
using ITMO.Buddy.Domain.Enums;
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

    public Task<Listing?> GetDetailsAsync(Guid listingId, CancellationToken cancellationToken) =>
        _context.Listings.AsNoTracking().Include(listing => listing.RequiredSkills)
            .SingleOrDefaultAsync(listing => listing.Id == listingId, cancellationToken);

    public Task<Listing?> GetForEditingAsync(Guid listingId, CancellationToken cancellationToken) =>
        _context.Listings.Include(listing => listing.RequiredSkills)
            .SingleOrDefaultAsync(listing => listing.Id == listingId, cancellationToken);

    public Task<Listing?> GetForRespondingAsync(Guid listingId, Guid candidateId, CancellationToken cancellationToken) =>
        _context.Listings.Include(listing => listing.Responses.Where(response => response.CandidateId == candidateId))
            .SingleOrDefaultAsync(listing => listing.Id == listingId, cancellationToken);

    public Task<Guid?> GetAuthorIdAsync(Guid listingId, CancellationToken cancellationToken) =>
        _context.Listings.AsNoTracking().Where(listing => listing.Id == listingId)
            .Select(listing => (Guid?)listing.AuthorId)
            .SingleOrDefaultAsync(cancellationToken);

    public Task<Listing?> GetByResponseIdAsync(
        Guid responseId,
        CancellationToken cancellationToken = default)
    {
        return _context.Listings
            .Include(listing => listing.Responses.Where(response => response.Id == responseId))
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

        return await VisibleListingResponses(listingId)
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
        return VisibleListingResponses(listingId).CountAsync(cancellationToken);
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

    private IQueryable<ListingResponse> VisibleListingResponses(Guid listingId) =>
        from response in _context.ListingResponses.AsNoTracking()
        join listing in _context.Listings on response.ListingId equals listing.Id
        where response.ListingId == listingId &&
              _context.Users.Any(user => user.Id == response.CandidateId && user.Status == UserStatus.Active) &&
              !_context.UserBlocks.Any(block =>
                  (block.BlockerId == listing.AuthorId && block.BlockedUserId == response.CandidateId) ||
                  (block.BlockerId == response.CandidateId && block.BlockedUserId == listing.AuthorId))
        select response;

    public void AddResponse(ListingResponse response) =>
        _context.ListingResponses.Add(response);

    public void RequireCurrentVersion(Listing listing) =>
        _context.Entry(listing).Property(item => item.UpdatedAtUtc).IsModified = true;

    public async Task RemoveResponseNotificationsAsync(Guid listingId, CancellationToken cancellationToken)
    {
        await _context.Notifications.Where(notification =>
            (notification.Type == NotificationType.ListingResponseCreated ||
             notification.Type == NotificationType.ListingResponseAccepted ||
             notification.Type == NotificationType.ListingResponseRejected) &&
            _context.ListingResponses.Any(response =>
                response.Id == notification.RelatedEntityId && response.ListingId == listingId))
            .ExecuteDeleteAsync(cancellationToken);
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