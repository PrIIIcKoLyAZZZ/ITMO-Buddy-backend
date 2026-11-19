using ITMO.Buddy.Domain.Users;

namespace ITMO.Buddy.Application.Abstractions.Persistence;

public interface IUserProfileRepository
{
    Task<UserProfile?> GetByIdAsync(
        Guid profileId,
        CancellationToken cancellationToken = default);

    Task<UserProfile?> GetByUserIdAsync(
        Guid userId,
        CancellationToken cancellationToken = default);

    void Add(UserProfile profile);

    void Remove(UserProfile profile);
}