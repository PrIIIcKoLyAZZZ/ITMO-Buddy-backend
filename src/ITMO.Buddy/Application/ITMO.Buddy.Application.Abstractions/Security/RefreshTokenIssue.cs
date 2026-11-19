using System.Text.Json.Serialization;

namespace ITMO.Buddy.Application.Abstractions.Security;

/// <summary>
/// Внутренний результат выпуска refresh-токена.
/// Не предназначен для публичной сериализации.
/// </summary>
public sealed class RefreshTokenIssue
{
    /// <summary>
    /// Открытое значение refresh-токена.
    /// Не сохраняется в базе данных.
    /// </summary>
    [JsonIgnore]
    public string Value { get; }

    /// <summary>
    /// Срок окончания действия refresh-токена.
    /// </summary>
    public DateTimeOffset ExpiresAtUtc { get; }

    public RefreshTokenIssue(
        string value,
        DateTimeOffset expiresAtUtc)
    {
        Value = value;
        ExpiresAtUtc = expiresAtUtc;
    }

    /// <summary>
    /// Исключает случайное раскрытие токена
    /// при обычном строковом логировании объекта.
    /// </summary>
    public override string ToString()
    {
        return nameof(RefreshTokenIssue);
    }
}