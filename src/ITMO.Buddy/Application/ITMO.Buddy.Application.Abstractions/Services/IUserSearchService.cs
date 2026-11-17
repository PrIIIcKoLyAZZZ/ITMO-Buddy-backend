using ITMO.Buddy.Application.Contracts.Common;
using ITMO.Buddy.Application.Contracts.Profiles;
using ITMO.Buddy.Application.Contracts.Search;

namespace ITMO.Buddy.Application.Abstractions.Services;

/// <summary>
/// Сервис поиска пользователей по тексту и фильтрам.
/// </summary>
public interface IUserSearchService
{
    /// <summary>
    /// Возвращает страницу карточек пользователей.
    /// Текущий пользователь определяется сервером.
    /// </summary>
    Task<PagedResult<ProfileCardResponse>> SearchAsync(
        SearchUsersRequest request,
        CancellationToken cancellationToken = default);
}