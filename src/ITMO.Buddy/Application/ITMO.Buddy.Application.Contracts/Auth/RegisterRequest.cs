namespace ITMO.Buddy.Application.Contracts.Auth;

/// <summary>
/// Запрос регистрации нового пользователя.
/// </summary>
public sealed record RegisterRequest
{
    public required string Email { get; init; }

    public required string Password { get; init; }

    public required bool ConsentToPersonalDataProcessing
    {
        get;
        init;
    }
}