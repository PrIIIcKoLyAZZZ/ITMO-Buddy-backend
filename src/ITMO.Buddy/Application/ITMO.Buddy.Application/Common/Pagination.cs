using ITMO.Buddy.Application.Abstractions.Persistence.Queries;
using ITMO.Buddy.Application.Contracts.Common;

namespace ITMO.Buddy.Application.Common;

internal static class Pagination
{
    public const int PageSize = PaginationRequest.DefaultPageSize;
    public const int MaxPage = int.MaxValue / PageSize;

    public static int GetOffset(int page) => checked((page - 1) * PageSize);

    public static PagedResult<TResponse> ToResponse<TItem, TResponse>(
        this QueryPage<TItem> page,
        Func<TItem, TResponse> map) =>
        new(page.Items.Select(map).ToArray(), page.Page, page.PageSize, page.TotalCount);
}
