namespace ITMO.Buddy.Application.Abstractions.Persistence;

public interface IUserPairLock
{
    /// <summary>Serializes writes for an unordered user pair until the caller's transaction ends.</summary>
    Task AcquireAsync(Guid firstUserId, Guid secondUserId, CancellationToken cancellationToken);

    /// <summary>
    /// Serializes read receipt writes for one recipient. If a user pair lock is needed,
    /// acquire it first. The self-pair key is reserved for this purpose.
    /// </summary>
    Task AcquireReadReceiptsAsync(Guid recipientId, CancellationToken cancellationToken) =>
        AcquireAsync(recipientId, recipientId, cancellationToken);
}
