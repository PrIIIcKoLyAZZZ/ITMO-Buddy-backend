using ITMO.Buddy.Application.Abstractions.Persistence;
using ITMO.Buddy.Domain.Interactions;
using ITMO.Buddy.Infrastructure.Persistence.Context;
using Microsoft.EntityFrameworkCore;

namespace ITMO.Buddy.Infrastructure.Persistence.Repositories;

public sealed class UserLikeRepository : IUserLikeRepository
{
    private readonly BuddyDbContext _context;

    public UserLikeRepository(BuddyDbContext context)
    {
        _context = context;
    }

    public Task<UserLike?> GetDirectedAsync(
        Guid senderId,
        Guid targetId,
        CancellationToken cancellationToken = default)
    {
        return _context.UserLikes
            .SingleOrDefaultAsync(
                x => x.SenderId == senderId &&
                     x.TargetId == targetId,
                cancellationToken);
    }

    public Task<bool> ExistsAsync(
        Guid senderId,
        Guid targetId,
        CancellationToken cancellationToken = default)
    {
        return _context.UserLikes
            .AsNoTracking()
            .AnyAsync(
                x => x.SenderId == senderId &&
                     x.TargetId == targetId,
                cancellationToken);
    }

    public async Task<IReadOnlyList<UserLike>> GetSentAsync(
        Guid senderId,
        int page,
        CancellationToken cancellationToken = default)
    {
        var skip = RepositoryPaging.GetSkip(page);

        return await _context.UserLikes
            .AsNoTracking()
            .Where(x => x.SenderId == senderId)
            .OrderByDescending(x => x.CreatedAtUtc)
            .ThenByDescending(x => x.Id)
            .Skip(skip)
            .Take(RepositoryPaging.PageSize)
            .ToListAsync(cancellationToken);
    }

    public void Add(UserLike like)
    {
        ArgumentNullException.ThrowIfNull(like);

        _context.UserLikes.Add(like);
    }

    public void Remove(UserLike like)
    {
        ArgumentNullException.ThrowIfNull(like);

        _context.UserLikes.Remove(like);
    }
}