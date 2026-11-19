namespace ITMO.Buddy.Application.Contracts.Dictionaries;

/// <summary>
/// Универсальный элемент справочника.
/// Используется для факультетов, интересов и навыков.
/// </summary>
public sealed record DictionaryItemResponse(
    Guid Id,
    string Name);