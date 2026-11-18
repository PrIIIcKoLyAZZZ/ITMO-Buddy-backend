namespace ITMO.Buddy.Application.Abstractions.Persistence;

public interface IFileCleanupQueue
{
    Task EnqueueAsync(string objectKey, CancellationToken cancellationToken);

    /// <summary>
    /// Claims an entry by deleting it inside the caller's transaction.
    /// The caller must commit only after deleting the object or making it live.
    /// A rollback restores the entry.
    /// </summary>
    Task<bool> TryTakeAsync(string objectKey, CancellationToken cancellationToken);

    Task<IReadOnlyList<FileCleanupCandidate>> GetCandidatesAsync(
        DateTimeOffset enqueuedNoLaterThanUtc,
        FileCleanupCandidate? after,
        int limit,
        CancellationToken cancellationToken);

    /// <summary>Claims the observed queue entry in the caller's transaction.</summary>
    Task<bool> TryTakeAsync(FileCleanupCandidate candidate, CancellationToken cancellationToken);

    Task<bool> HasProfileReferenceAsync(string objectKey, CancellationToken cancellationToken);

    Task<int> EnqueueUnreferencedAsync(
        IReadOnlyCollection<string> objectKeys,
        CancellationToken cancellationToken);
}
