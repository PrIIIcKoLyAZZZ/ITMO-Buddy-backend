namespace ITMO.Buddy.Application.Contracts.Likes;

/// <summary>
/// Результат установки или удаления лайка.
/// </summary>
public sealed record LikeResponse(
    Guid TargetUserId,
    bool IsLiked,
    bool IsMatch)
{
    /// <summary>
    /// Информация о взаимном мэтче.
    /// Null, если взаимного лайка нет.
    /// </summary>
    public MatchResponse? Match { get; init; }
}