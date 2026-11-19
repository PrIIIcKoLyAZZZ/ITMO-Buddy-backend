using ITMO.Buddy.Domain.Interactions;

namespace ITMO.Buddy.Application.Abstractions.Persistence;

public interface IUserLikeRepository
{
    Task<UserLike?> GetDirectedAsync(
        Guid senderId,
        Guid targetId,
        CancellationToken cancellationToken = default);

    Task<bool> ExistsAsync(
        Guid senderId,
        Guid targetId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<UserLike>> GetSentAsync(
        Guid senderId,
        int page,
        CancellationToken cancellationToken = default);

    void Add(UserLike like);

    void Remove(UserLike like);
}