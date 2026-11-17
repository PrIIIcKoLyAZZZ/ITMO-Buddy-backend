using ITMO.Buddy.Application.Abstractions.Persistence;
using ITMO.Buddy.Domain.Interactions;
using ITMO.Buddy.Infrastructure.Persistence.Context;
using Microsoft.EntityFrameworkCore;

namespace ITMO.Buddy.Infrastructure.Persistence.Repositories;

public sealed class UserMatchRepository : IUserMatchRepository
{
    private readonly BuddyDbContext _context;

    public UserMatchRepository(BuddyDbContext context)
    {
        _context = context;
    }

    public Task<UserMatch?> GetBetweenAsync(
        Guid firstUserId,
        Guid secondUserId,
        CancellationToken cancellationToken = default)
    {
        return _context.UserMatches
            .SingleOrDefaultAsync(
                x =>
                    (x.FirstUserId == firstUserId &&
                     x.SecondUserId == secondUserId) ||
                    (x.FirstUserId == secondUserId &&
                     x.SecondUserId == firstUserId),
                cancellationToken);
    }

    public Task<bool> ExistsBetweenAsync(
        Guid firstUserId,
        Guid secondUserId,
        CancellationToken cancellationToken = default)
    {
        return _context.UserMatches
            .AsNoTracking()
            .AnyAsync(
                x =>
                    (x.FirstUserId == firstUserId &&
                     x.SecondUserId == secondUserId) ||
                    (x.FirstUserId == secondUserId &&
                     x.SecondUserId == firstUserId),
                cancellationToken);
    }

    public async Task<IReadOnlyList<UserMatch>>
        GetForUserAsync(
            Guid userId,
            int page,
            CancellationToken cancellationToken = default)
    {
        var skip = RepositoryPaging.GetSkip(page);

        return await _context.UserMatches
            .AsNoTracking()
            .Where(x =>
                x.FirstUserId == userId ||
                x.SecondUserId == userId)
            .OrderByDescending(x => x.CreatedAtUtc)
            .ThenByDescending(x => x.Id)
            .Skip(skip)
            .Take(RepositoryPaging.PageSize)
            .ToListAsync(cancellationToken);
    }

    public void Add(UserMatch match)
    {
        ArgumentNullException.ThrowIfNull(match);

        _context.UserMatches.Add(match);
    }

    public void Remove(UserMatch match)
    {
        ArgumentNullException.ThrowIfNull(match);

        _context.UserMatches.Remove(match);
    }
}