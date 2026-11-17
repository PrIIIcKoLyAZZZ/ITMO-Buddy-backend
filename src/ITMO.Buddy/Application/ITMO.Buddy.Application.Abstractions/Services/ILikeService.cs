using ITMO.Buddy.Application.Contracts.Likes;

namespace ITMO.Buddy.Application.Abstractions.Services;

/// <summary>
/// Сервис лайков и создания взаимных мэтчей.
/// </summary>
public interface ILikeService
{
    /// <summary>
    /// Устанавливает лайк целевому пользователю.
    /// При взаимном лайке создаёт или находит мэтч.
    /// </summary>
    Task<LikeResponse> LikeAsync(
        Guid targetUserId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Удаляет исходящий лайк текущего пользователя.
    /// </summary>
    Task<LikeResponse> UnlikeAsync(
        Guid targetUserId,
        CancellationToken cancellationToken = default);
}