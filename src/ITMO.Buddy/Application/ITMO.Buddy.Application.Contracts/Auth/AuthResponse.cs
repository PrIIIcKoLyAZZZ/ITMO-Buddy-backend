namespace ITMO.Buddy.Application.Contracts.Auth;

/// <summary>
/// Публичный результат успешной аутентификации.
/// Refresh token в JSON не передаётся.
/// </summary>
public sealed record AuthResponse(
    Guid UserId,
    string AccessToken,
    DateTimeOffset ExpiresAtUtc);