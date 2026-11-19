using ITMO.Buddy.Application.Abstractions.Recommendations;
using Microsoft.Extensions.Caching.Memory;

namespace ITMO.Buddy.Application.Recommendations;

internal sealed class MemoryRecommendationSkipStore : IRecommendationSkipStore, IDisposable
{
    private readonly MemoryCache _cache = new(new MemoryCacheOptions());
    private readonly Lock _writeLock = new();

    public Task SkipAsync(
        Guid userId,
        Guid targetUserId,
        DateTimeOffset expiresAtUtc,
        CancellationToken cancellationToken = default)
    {
        var key = (userId, targetUserId);

        lock (_writeLock)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!_cache.TryGetValue(key, out _))
            {
                _cache.Set(key, expiresAtUtc, expiresAtUtc);
            }
        }

        return Task.CompletedTask;
    }

    public Task<IReadOnlySet<Guid>> GetActiveSkippedIdsAsync(
        Guid userId,
        IReadOnlyCollection<Guid> candidateUserIds,
        DateTimeOffset nowUtc,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var skipped = new HashSet<Guid>();
        foreach (var targetUserId in candidateUserIds)
        {
            if (_cache.TryGetValue<DateTimeOffset>((userId, targetUserId), out var expiration) &&
                expiration > nowUtc)
            {
                skipped.Add(targetUserId);
            }
        }

        return Task.FromResult<IReadOnlySet<Guid>>(skipped);
    }

    public void Dispose() => _cache.Dispose();
}
