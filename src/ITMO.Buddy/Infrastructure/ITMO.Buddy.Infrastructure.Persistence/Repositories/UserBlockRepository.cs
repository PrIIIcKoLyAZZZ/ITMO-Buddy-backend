using ITMO.Buddy.Application.Abstractions.Persistence;
using ITMO.Buddy.Domain.Interactions;
using ITMO.Buddy.Infrastructure.Persistence.Context;
using Microsoft.EntityFrameworkCore;

namespace ITMO.Buddy.Infrastructure.Persistence.Repositories;

public sealed class UserBlockRepository : IUserBlockRepository
{
    private readonly BuddyDbContext _context;

    public UserBlockRepository(BuddyDbContext context)
    {
        _context = context;
    }

    public Task<UserBlock?> GetDirectedAsync(
        Guid blockerId,
        Guid blockedUserId,
        CancellationToken cancellationToken = default)
    {
        return _context.UserBlocks
            .SingleOrDefaultAsync(
                x => x.BlockerId == blockerId &&
                     x.BlockedUserId == blockedUserId,
                cancellationToken);
    }

    public Task<bool> ExistsInEitherDirectionAsync(
        Guid firstUserId,
        Guid secondUserId,
        CancellationToken cancellationToken = default)
    {
        return _context.UserBlocks
            .AsNoTracking()
            .AnyAsync(
                x =>
                    (x.BlockerId == firstUserId &&
                     x.BlockedUserId == secondUserId) ||
                    (x.BlockerId == secondUserId &&
                     x.BlockedUserId == firstUserId),
                cancellationToken);
    }

    public async Task<IReadOnlyList<UserBlock>>
        GetByBlockerAsync(
            Guid blockerId,
            int page,
            CancellationToken cancellationToken = default)
    {
        var skip = RepositoryPaging.GetSkip(page);

        return await _context.UserBlocks
            .AsNoTracking()
            .Where(x => x.BlockerId == blockerId)
            .OrderByDescending(x => x.CreatedAtUtc)
            .ThenByDescending(x => x.Id)
            .Skip(skip)
            .Take(RepositoryPaging.PageSize)
            .ToListAsync(cancellationToken);
    }

    public void Add(UserBlock block)
    {
        ArgumentNullException.ThrowIfNull(block);

        _context.UserBlocks.Add(block);
    }

    public void Remove(UserBlock block)
    {
        ArgumentNullException.ThrowIfNull(block);

        _context.UserBlocks.Remove(block);
    }
}