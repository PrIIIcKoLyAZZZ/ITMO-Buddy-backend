using ITMO.Buddy.Domain.Users;

namespace ITMO.Buddy.Application.Abstractions.Persistence;

public interface IRefreshTokenRepository
{
    Task<RefreshToken?> GetByHashAsync(
        string tokenHash,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<RefreshToken>> GetByUserIdAsync(
        Guid userId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<RefreshToken>> GetExpiredAsync(
        DateTimeOffset nowUtc,
        int take,
        CancellationToken cancellationToken = default);

    void Add(RefreshToken token);

    void Remove(RefreshToken token);
}