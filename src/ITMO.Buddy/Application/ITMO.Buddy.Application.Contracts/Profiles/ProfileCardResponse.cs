using ITMO.Buddy.Application.Contracts.Dictionaries;

namespace ITMO.Buddy.Application.Contracts.Profiles;

/// <summary>
/// Облегчённая карточка пользователя для поиска
/// и рекомендаций.
/// </summary>
public sealed record ProfileCardResponse(
    Guid UserId,
    string? FirstName,
    string? LastName,
    string? PhotoUrl,
    int? Course,
    DictionaryItemResponse? Faculty,
    IReadOnlyList<string> Goals,
    IReadOnlyList<DictionaryItemResponse> Interests);