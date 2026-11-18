namespace ITMO.Buddy.Infrastructure.Persistence.Models;

internal sealed class FileCleanupEntry
{
    public string ObjectKey { get; private set; } = null!;

    public DateTimeOffset EnqueuedAtUtc { get; private set; }
}
