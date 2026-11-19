using ITMO.Buddy.Domain.Enums;

namespace ITMO.Buddy.Application.Abstractions.Security;

/// <summary>
/// Абстракция выпуска и хеширования токенов.
/// Не отвечает за сохранение токенов в БД.
/// </summary>
public interface ITokenService
{
    /// <summary>
    /// Создаёт подписанный JWT access token.
    /// Срок действия не должен превышать 30 минут.
    /// </summary>
    AccessTokenIssue CreateAccessToken(
        Guid userId,
        UserRole role,
        DateTimeOffset issuedAtUtc);

    /// <summary>
    /// Создаёт криптографически стойкий
    /// случайный refresh-токен.
    /// Срок действия — 14 дней.
    /// </summary>
    RefreshTokenIssue CreateRefreshToken(
        DateTimeOffset issuedAtUtc);

    /// <summary>
    /// Вычисляет детерминированный
    /// криптографический хеш refresh-токена.
    /// </summary>
    string HashRefreshToken(
        string refreshToken);
}