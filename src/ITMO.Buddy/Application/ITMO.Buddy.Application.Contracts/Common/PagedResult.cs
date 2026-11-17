namespace ITMO.Buddy.Application.Contracts.Common;

/// <summary>
/// Универсальный результат постраничного запроса.
/// </summary>
/// <typeparam name="T">Тип элементов страницы.</typeparam>
public sealed record PagedResult<T>(
    IReadOnlyList<T> Items,
    int Page,
    int PageSize,
    int TotalCount)
{
    public bool HasNextPage =>
        (long)Page * PageSize < TotalCount;
}