using ITMO.Buddy.Application.Abstractions.Persistence;
using ITMO.Buddy.Infrastructure.Persistence.Context;
using ITMO.Buddy.Infrastructure.Persistence.Models;
using Microsoft.EntityFrameworkCore;

namespace ITMO.Buddy.Infrastructure.Persistence.Repositories;

internal sealed class FileCleanupQueue(BuddyDbContext context) : IFileCleanupQueue
{
    public async Task EnqueueAsync(string objectKey, CancellationToken cancellationToken)
    {
        await context.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO file_cleanup_queue (object_key)
            VALUES ({objectKey})
            ON CONFLICT (object_key) DO NOTHING
            """, cancellationToken);
    }

    public async Task<bool> TryTakeAsync(string objectKey, CancellationToken cancellationToken) =>
        await context.Set<FileCleanupEntry>().Where(entry => entry.ObjectKey == objectKey)
            .ExecuteDeleteAsync(cancellationToken) != 0;

    public async Task<IReadOnlyList<FileCleanupCandidate>> GetCandidatesAsync(
        DateTimeOffset enqueuedNoLaterThanUtc,
        FileCleanupCandidate? after,
        int limit,
        CancellationToken cancellationToken)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(limit, 1);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(limit, 100);
        var cutoffUtc = enqueuedNoLaterThanUtc.ToUniversalTime();
        var cursorTimeUtc = after?.EnqueuedAtUtc.ToUniversalTime() ?? cutoffUtc;
        var cursorKey = after?.ObjectKey ?? string.Empty;
        var hasCursor = after is not null;

        return await context.Database.SqlQuery<FileCleanupCandidate>($"""
            SELECT q.object_key AS "ObjectKey", q.enqueued_at_utc AS "EnqueuedAtUtc"
            FROM file_cleanup_queue AS q
            WHERE q.enqueued_at_utc <= {cutoffUtc}
              AND (NOT {hasCursor} OR
                   (q.enqueued_at_utc, q.object_key) > ({cursorTimeUtc}, {cursorKey}))
              AND NOT EXISTS (
                  SELECT 1 FROM user_profiles AS p WHERE p.photo_object_key = q.object_key
              )
            ORDER BY q.enqueued_at_utc, q.object_key
            LIMIT {limit}
            """).ToListAsync(cancellationToken);
    }

    public async Task<bool> TryTakeAsync(FileCleanupCandidate candidate, CancellationToken cancellationToken)
    {
        var enqueuedAtUtc = candidate.EnqueuedAtUtc.ToUniversalTime();
        return await context.Set<FileCleanupEntry>()
            .Where(entry => entry.ObjectKey == candidate.ObjectKey && entry.EnqueuedAtUtc == enqueuedAtUtc)
            .ExecuteDeleteAsync(cancellationToken) != 0;
    }

    public Task<bool> HasProfileReferenceAsync(string objectKey, CancellationToken cancellationToken) =>
        context.UserProfiles.AnyAsync(profile => profile.PhotoObjectKey == objectKey, cancellationToken);

    public Task<int> EnqueueUnreferencedAsync(
        IReadOnlyCollection<string> objectKeys,
        CancellationToken cancellationToken)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(objectKeys.Count, 1);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(objectKeys.Count, 100);
        var keys = objectKeys.ToArray();

        return context.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO file_cleanup_queue (object_key)
            SELECT DISTINCT pending.object_key
            FROM unnest({keys}::text[]) AS pending(object_key)
            WHERE NOT EXISTS (
                SELECT 1 FROM user_profiles AS p WHERE p.photo_object_key = pending.object_key
            )
              AND NOT EXISTS (
                SELECT 1 FROM file_cleanup_queue AS q WHERE q.object_key = pending.object_key
            )
            ORDER BY pending.object_key
            ON CONFLICT (object_key) DO NOTHING
            """, cancellationToken);
    }
}
