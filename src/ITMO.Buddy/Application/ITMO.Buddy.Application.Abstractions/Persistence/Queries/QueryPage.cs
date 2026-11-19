namespace ITMO.Buddy.Application.Abstractions.Persistence.Queries;

public sealed record QueryPage<T>(
    IReadOnlyList<T> Items,
    int Page,
    int PageSize,
    int TotalCount);