using ITMO.Buddy.Application.Abstractions.Persistence;
using ITMO.Buddy.Domain.Users;
using ITMO.Buddy.Infrastructure.Persistence.Context;
using Microsoft.EntityFrameworkCore;

namespace ITMO.Buddy.Infrastructure.Persistence.Repositories;

public sealed class RefreshTokenRepository
    : IRefreshTokenRepository
{
    private readonly BuddyDbContext _context;

    public RefreshTokenRepository(BuddyDbContext context)
    {
        _context = context;
    }

    public Task<RefreshToken?> GetByHashAsync(
        string tokenHash,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(
            tokenHash);

        return _context.RefreshTokens
            .FirstOrDefaultAsync(
                x => x.TokenHash == tokenHash,
                cancellationToken);
    }

    public async Task<IReadOnlyList<RefreshToken>> GetByUserIdAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        return await _context.RefreshTokens
            .Where(x => x.UserId == userId)
            .OrderByDescending(x => x.CreatedAtUtc)
            .ThenByDescending(x => x.Id)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<RefreshToken>> GetExpiredAsync(
        DateTimeOffset nowUtc,
        int take,
        CancellationToken cancellationToken = default)
    {
        if (nowUtc.Offset != TimeSpan.Zero)
        {
            throw new ArgumentException(
                "Timestamp must be UTC.",
                nameof(nowUtc));
        }

        if (take <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(take),
                "Batch size must be positive.");
        }

        return await _context.RefreshTokens
            .Where(x => x.ExpiresAtUtc <= nowUtc)
            .OrderBy(x => x.ExpiresAtUtc)
            .ThenBy(x => x.Id)
            .Take(take)
            .ToListAsync(cancellationToken);
    }

    public void Add(RefreshToken token)
    {
        ArgumentNullException.ThrowIfNull(token);

        _context.RefreshTokens.Add(token);
    }

    public void Remove(RefreshToken token)
    {
        ArgumentNullException.ThrowIfNull(token);

        _context.RefreshTokens.Remove(token);
    }
}