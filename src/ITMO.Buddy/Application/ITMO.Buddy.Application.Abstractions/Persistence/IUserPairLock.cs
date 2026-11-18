namespace ITMO.Buddy.Application.Abstractions.Persistence;

public interface IUserPairLock
{
    /// <summary>Serializes writes for an unordered user pair until the caller's transaction ends.</summary>
    Task AcquireAsync(Guid firstUserId, Guid secondUserId, CancellationToken cancellationToken);
}
