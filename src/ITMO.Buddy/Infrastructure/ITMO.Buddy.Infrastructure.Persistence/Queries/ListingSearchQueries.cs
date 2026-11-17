using ITMO.Buddy.Application.Abstractions.Persistence.Queries;
using ITMO.Buddy.Domain.Enums;
using ITMO.Buddy.Domain.Listings;
using ITMO.Buddy.Infrastructure.Persistence.Context;
using ITMO.Buddy.Infrastructure.Persistence.Repositories;
using Microsoft.EntityFrameworkCore;
using NpgsqlTypes;

namespace ITMO.Buddy.Infrastructure.Persistence.Queries;

public sealed class ListingSearchQueries : IListingSearchQueries
{
    private readonly BuddyDbContext _context;

    public ListingSearchQueries(BuddyDbContext context)
    {
        _context = context;
    }

    public async Task<QueryPage<ListingSearchItem>> SearchPublicAsync(
        ListingSearchFilter filter,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(filter);

        if (filter.NowUtc == default ||
            filter.NowUtc.Offset != TimeSpan.Zero)
        {
            throw new ArgumentException(
                "Current timestamp must be valid UTC.",
                nameof(filter));
        }

        if (filter.Category is { } category &&
            !Enum.IsDefined(category))
        {
            throw new ArgumentException(
                "Unknown listing category.",
                nameof(filter));
        }

        if (filter.Format is { } format &&
            !Enum.IsDefined(format))
        {
            throw new ArgumentException(
                "Unknown listing format.",
                nameof(filter));
        }

        var text = SearchText.Normalize(filter.Text);
        var nowUtc = filter.NowUtc;

        var query = _context.Listings
            .AsNoTracking()
            .Where(listing =>
                listing.Status == ListingStatus.Open &&
                listing.CreatedAtUtc <= nowUtc &&
                (listing.DeadlineUtc == null ||
                 listing.DeadlineUtc > nowUtc) &&
                (listing.EventAtUtc == null ||
                 listing.EventAtUtc > nowUtc));

        if (text is not null)
        {
            var pattern = SearchText.ToContainsPattern(text);
            var useFullTextSearch = text.Length >= 3;

            query = query.Where(listing =>
                (useFullTextSearch &&
                 EF.Property<NpgsqlTsVector>(
                     listing, "SearchVector")
                     .Matches(
                         EF.Functions.PlainToTsQuery(
                             "russian", text))) ||

                EF.Functions.ILike(
                    listing.Title, pattern, "\\") ||

                EF.Functions.ILike(
                    listing.Description, pattern, "\\"));
        }

        if (filter.Category is GoalCategory selectedCategory)
        {
            query = query.Where(listing =>
                listing.Category == selectedCategory);
        }

        if (filter.Format is ListingFormat selectedFormat)
        {
            query = query.Where(listing =>
                listing.Format == selectedFormat);
        }

        var skillIds = filter.SkillIds?
            .Distinct()
            .ToArray() ?? Array.Empty<Guid>();

        if (skillIds.Length > 0)
        {
            query = query.Where(listing =>
                listing.RequiredSkills.Any(skill =>
                    skillIds.Contains(skill.Id)));
        }

        return await ToPageAsync(
            query,
            filter.Page,
            cancellationToken);
    }

    public Task<QueryPage<ListingSearchItem>> GetByAuthorAsync(
        Guid authorId,
        int page,
        CancellationToken cancellationToken = default)
    {
        if (authorId == Guid.Empty)
        {
            throw new ArgumentException(
                "Author identifier is required.",
                nameof(authorId));
        }

        var query = _context.Listings
            .AsNoTracking()
            .Where(listing =>
                listing.AuthorId == authorId);

        return ToPageAsync(
            query,
            page,
            cancellationToken);
    }

    private static async Task<QueryPage<ListingSearchItem>> ToPageAsync(
        IQueryable<Listing> query,
        int page,
        CancellationToken cancellationToken)
    {
        var skip = RepositoryPaging.GetSkip(page);

        var totalCount = await query.CountAsync(
            cancellationToken);

        var items = await query
            .OrderByDescending(x => x.CreatedAtUtc)
            .ThenByDescending(x => x.Id)
            .Skip(skip)
            .Take(RepositoryPaging.PageSize)
            .Select(x => new ListingSearchItem(
                x.Id,
                x.AuthorId,
                x.Title,
                x.Description,
                x.Category,
                x.Format,
                x.Status,
                x.RequiredParticipantsCount,
                x.AcceptedParticipantsCount,
                x.CreatedAtUtc,
                x.DeadlineUtc,
                x.EventAtUtc))
            .ToListAsync(cancellationToken);

        return new QueryPage<ListingSearchItem>(
            items,
            page,
            RepositoryPaging.PageSize,
            totalCount);
    }
}