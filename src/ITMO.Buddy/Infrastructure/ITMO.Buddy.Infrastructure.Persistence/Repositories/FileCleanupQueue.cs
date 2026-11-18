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
}
