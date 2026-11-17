namespace ITMO.Buddy.Application.Contracts.Common;

/// <summary>
/// Ошибки валидации конкретного поля запроса.
/// </summary>
/// <param name="Field">Название поля.</param>
/// <param name="Messages">Сообщения об ошибках.</param>
public sealed record ValidationError(
    string Field,
    IReadOnlyList<string> Messages);