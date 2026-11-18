namespace ITMO.Buddy.Application.Abstractions.Persistence;

public sealed record FileCleanupCandidate(string ObjectKey, DateTimeOffset EnqueuedAtUtc);
