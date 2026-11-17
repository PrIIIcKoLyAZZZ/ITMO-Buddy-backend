namespace ITMO.Buddy.Application.Contracts.Auth;

/// <summary>
/// Запрос аутентификации по email и паролю.
/// </summary>
public sealed record LoginRequest
{
    public required string Email { get; init; }

    public required string Password { get; init; }
}