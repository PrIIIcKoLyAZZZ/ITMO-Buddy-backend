namespace ITMO.Buddy.Application.Abstractions.Security;

/// <summary>
/// Результат выпуска JWT access token.
/// </summary>
/// <param name="Token">
/// Подписанный JWT access token.
/// </param>
/// <param name="ExpiresAtUtc">
/// Время окончания действия токена в UTC.
/// </param>
public sealed record AccessTokenIssue(
    string Token,
    DateTimeOffset ExpiresAtUtc);