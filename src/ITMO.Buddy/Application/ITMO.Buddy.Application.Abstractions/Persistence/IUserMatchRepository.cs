using ITMO.Buddy.Domain.Interactions;

namespace ITMO.Buddy.Application.Abstractions.Persistence;

public interface IUserMatchRepository
{
    Task<UserMatch?> GetBetweenAsync(
        Guid firstUserId,
        Guid secondUserId,
        CancellationToken cancellationToken = default);

    Task<bool> ExistsBetweenAsync(
        Guid firstUserId,
        Guid secondUserId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<UserMatch>> GetForUserAsync(
        Guid userId,
        int page,
        CancellationToken cancellationToken = default);

    void Add(UserMatch match);

    void Remove(UserMatch match);
}