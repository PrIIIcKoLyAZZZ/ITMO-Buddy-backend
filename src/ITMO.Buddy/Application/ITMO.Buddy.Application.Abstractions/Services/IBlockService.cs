namespace ITMO.Buddy.Application.Abstractions.Services;

/// <summary>
/// Сервис управления блокировками пользователей.
/// </summary>
public interface IBlockService
{
    /// <summary>
    /// Блокирует целевого пользователя.
    /// Инициатор определяется из ICurrentUser.
    /// </summary>
    Task BlockAsync(
        Guid targetUserId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Снимает блокировку целевого пользователя.
    /// </summary>
    Task UnblockAsync(
        Guid targetUserId,
        CancellationToken cancellationToken = default);
}