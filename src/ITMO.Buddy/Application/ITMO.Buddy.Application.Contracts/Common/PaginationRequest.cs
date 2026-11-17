namespace ITMO.Buddy.Application.Contracts.Common;

/// <summary>
/// Общие параметры постраничного получения данных.
/// Нумерация страниц начинается с 1.
/// </summary>
public sealed record PaginationRequest
{
    public const int DefaultPageSize = 20;

    public int Page { get; init; } = 1;

    public int PageSize { get; init; } = DefaultPageSize;
}