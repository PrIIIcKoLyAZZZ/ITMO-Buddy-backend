namespace ITMO.Buddy.Application.Contracts.Dictionaries;

/// <summary>
/// Параметры получения справочных значений.
/// Поддерживает поиск для автодополнения.
/// </summary>
public sealed record DictionarySearchRequest
{
    public string? SearchText { get; init; }

    public int Limit { get; init; } = 20;
}