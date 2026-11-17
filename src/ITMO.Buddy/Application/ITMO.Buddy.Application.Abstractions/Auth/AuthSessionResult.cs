using System.Text.Json.Serialization;
using ITMO.Buddy.Application.Contracts.Auth;

namespace ITMO.Buddy.Application.Abstractions.Auth;

/// <summary>
/// Внутренний результат создания или обновления сессии.
/// Не является публичным API DTO.
/// </summary>
public sealed class AuthSessionResult
{
    /// <summary>
    /// Данные, которые API возвращает клиенту в JSON.
    /// </summary>
    public AuthResponse Response { get; }

    /// <summary>
    /// Открытое значение refresh-токена.
    /// Используется только для установки HttpOnly cookie.
    /// Не должно попадать в JSON и логи.
    /// </summary>
    [JsonIgnore]
    public string RefreshToken { get; }

    /// <summary>
    /// Срок действия refresh-токена.
    /// Используется для настройки cookie.
    /// </summary>
    [JsonIgnore]
    public DateTimeOffset RefreshTokenExpiresAtUtc
    {
        get;
    }

    public AuthSessionResult(
        AuthResponse response,
        string refreshToken,
        DateTimeOffset refreshTokenExpiresAtUtc)
    {
        Response = response;
        RefreshToken = refreshToken;
        RefreshTokenExpiresAtUtc =
            refreshTokenExpiresAtUtc;
    }

    public override string ToString()
    {
        return nameof(AuthSessionResult);
    }
}