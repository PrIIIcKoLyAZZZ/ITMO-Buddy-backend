namespace ITMO.Buddy.Application.Abstractions.Persistence;

public interface IAccountMaintenanceRepository
{
    /// <summary>
    /// Locks the account until the enclosing transaction ends.
    /// </summary>
    Task<bool> LockAsync(Guid userId, CancellationToken cancellationToken);

    /// <summary>
    /// Physically deletes the locked account and its dependent data in the enclosing transaction.
    /// </summary>
    Task DeleteAsync(Guid userId, CancellationToken cancellationToken);
}
