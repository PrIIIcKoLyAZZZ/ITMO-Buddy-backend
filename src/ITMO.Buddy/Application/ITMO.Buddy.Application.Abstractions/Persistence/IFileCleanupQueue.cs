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
}
