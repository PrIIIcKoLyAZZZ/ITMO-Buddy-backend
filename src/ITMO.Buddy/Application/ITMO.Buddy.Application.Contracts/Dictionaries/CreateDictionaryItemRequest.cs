namespace ITMO.Buddy.Application.Contracts.Dictionaries;

/// <summary>
/// Запрос создания пользовательского интереса или навыка.
/// </summary>
public sealed record CreateDictionaryItemRequest
{
    public required string Name { get; init; }
}