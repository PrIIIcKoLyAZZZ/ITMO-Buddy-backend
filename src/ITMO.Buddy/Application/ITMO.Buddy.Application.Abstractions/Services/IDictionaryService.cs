using ITMO.Buddy.Application.Contracts.Dictionaries;

namespace ITMO.Buddy.Application.Abstractions.Services;

/// <summary>
/// Сервис факультетов, интересов и навыков.
/// </summary>
public interface IDictionaryService
{
    /// <summary>
    /// Возвращает список факультетов.
    /// </summary>
    Task<IReadOnlyList<DictionaryItemResponse>>
        GetFacultiesAsync(
            CancellationToken cancellationToken = default);

    /// <summary>
    /// Ищет интересы для автодополнения.
    /// </summary>
    Task<IReadOnlyList<DictionaryItemResponse>>
        SearchInterestsAsync(
            DictionarySearchRequest request,
            CancellationToken cancellationToken = default);

    /// <summary>
    /// Ищет навыки для автодополнения.
    /// </summary>
    Task<IReadOnlyList<DictionaryItemResponse>>
        SearchSkillsAsync(
            DictionarySearchRequest request,
            CancellationToken cancellationToken = default);

    /// <summary>
    /// Создаёт пользовательский интерес
    /// либо возвращает существующий
    /// с таким же нормализованным названием.
    /// </summary>
    Task<DictionaryItemResponse> CreateInterestAsync(
        CreateDictionaryItemRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Создаёт пользовательский навык
    /// либо возвращает существующий
    /// с таким же нормализованным названием.
    /// </summary>
    Task<DictionaryItemResponse> CreateSkillAsync(
        CreateDictionaryItemRequest request,
        CancellationToken cancellationToken = default);
}