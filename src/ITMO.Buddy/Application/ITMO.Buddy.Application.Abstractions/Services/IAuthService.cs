using ITMO.Buddy.Application.Abstractions.Auth;
using ITMO.Buddy.Application.Contracts.Auth;

namespace ITMO.Buddy.Application.Abstractions.Services;

/// <summary>
/// Сервис регистрации, аутентификации и управления
/// пользовательскими сессиями.
/// </summary>
public interface IAuthService
{
    /// <summary>
    /// Регистрирует нового пользователя и создаёт сессию.
    /// </summary>
    Task<AuthSessionResult> RegisterAsync(
        RegisterRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Проверяет учётные данные и создаёт сессию.
    /// </summary>
    Task<AuthSessionResult> LoginAsync(
        LoginRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Обновляет access token с ротацией refresh-токена.
    /// Исходное значение приходит из HttpOnly cookie,
    /// но сервис не зависит от HTTP.
    /// </summary>
    Task<AuthSessionResult> RefreshAsync(
        string refreshToken,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Отзывает текущую refresh-сессию.
    /// Null допускается для идемпотентного logout.
    /// </summary>
    Task LogoutAsync(
        string? refreshToken,
        CancellationToken cancellationToken = default);
}