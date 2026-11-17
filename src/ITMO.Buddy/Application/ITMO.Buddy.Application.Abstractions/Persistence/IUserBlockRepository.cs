using ITMO.Buddy.Domain.Interactions;

namespace ITMO.Buddy.Application.Abstractions.Persistence;

public interface IUserBlockRepository
{
    Task<UserBlock?> GetDirectedAsync(
        Guid blockerId,
        Guid blockedUserId,
        CancellationToken cancellationToken = default);

    Task<bool> ExistsInEitherDirectionAsync(
        Guid firstUserId,
        Guid secondUserId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<UserBlock>> GetByBlockerAsync(
        Guid blockerId,
        int page,
        CancellationToken cancellationToken = default);

    void Add(UserBlock block);

    void Remove(UserBlock block);
}