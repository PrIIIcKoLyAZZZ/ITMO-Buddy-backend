using ITMO.Buddy.Application.Contracts.Dictionaries;

namespace ITMO.Buddy.Application.Contracts.Profiles;

/// <summary>
/// Полная публичная модель профиля пользователя.
/// </summary>
public sealed record ProfileResponse(
    Guid Id,
    Guid UserId,
    string? FirstName,
    string? LastName,
    string? PhotoUrl,
    DictionaryItemResponse? Faculty,
    int? Course,
    string? AboutMe,
    string? Contact,
    IReadOnlyList<string> Goals,
    IReadOnlyList<DictionaryItemResponse> Interests,
    IReadOnlyList<DictionaryItemResponse> Skills,
    bool IsVisible,
    bool IsComplete);