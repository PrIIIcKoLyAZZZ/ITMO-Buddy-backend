using ITMO.Buddy.Domain.Enums;

namespace ITMO.Buddy.Application.Abstractions.Persistence.Queries;

public sealed record ListingSearchFilter
{
    public required DateTimeOffset NowUtc { get; init; }

    public Guid? ViewerId { get; init; }

    public string? Text { get; init; }

    public GoalCategory? Category { get; init; }

    public ListingFormat? Format { get; init; }

    public IReadOnlyCollection<Guid>? SkillIds
    {
        get;
        init;
    }

    public int Page { get; init; } = 1;
}

public sealed record ListingSearchItem(
    Guid Id,
    Guid AuthorId,
    string Title,
    string Description,
    GoalCategory Category,
    ListingFormat Format,
    ListingStatus Status,
    int RequiredParticipantsCount,
    int AcceptedParticipantsCount,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset? DeadlineUtc,
    DateTimeOffset? EventAtUtc);

public sealed record ListingAuthorItem(
    Guid UserId,
    string? FirstName,
    string? LastName,
    string? PhotoObjectKey);

public interface IListingSearchQueries
{
    Task<QueryPage<ListingSearchItem>> SearchPublicAsync(
        ListingSearchFilter filter,
        CancellationToken cancellationToken = default);

    Task<QueryPage<ListingSearchItem>> GetByAuthorAsync(
        Guid authorId,
        int page,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ListingAuthorItem>> GetAuthorsAsync(
        Guid viewerId,
        IReadOnlyCollection<Guid> authorIds,
        CancellationToken cancellationToken);
}