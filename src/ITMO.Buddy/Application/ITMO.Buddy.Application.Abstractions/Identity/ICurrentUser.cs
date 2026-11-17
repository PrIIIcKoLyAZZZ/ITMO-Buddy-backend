using ITMO.Buddy.Domain.Enums;

namespace ITMO.Buddy.Application.Abstractions.Identity;

/// <summary>
/// Доверенный контекст текущего пользователя.
/// Не зависит от HttpContext и ASP.NET Core.
/// </summary>
public interface ICurrentUser
{
    /// <summary>
    /// Признак успешной аутентификации.
    /// </summary>
    bool IsAuthenticated { get; }

    /// <summary>
    /// Идентификатор текущего пользователя.
    /// Null для неаутентифицированного запроса.
    /// </summary>
    Guid? UserId { get; }

    /// <summary>
    /// Роль текущего пользователя.
    /// Null при отсутствии аутентификации.
    /// </summary>
    UserRole? Role { get; }
}