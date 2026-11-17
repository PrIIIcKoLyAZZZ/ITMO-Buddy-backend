using ITMO.Buddy.Domain.Enums;

namespace ITMO.Buddy.Application.Abstractions.Persistence.Queries;

public sealed record UserSearchFilter
{
    public required Guid CurrentUserId { get; init; }

    public string? Text { get; init; }

    public IReadOnlyCollection<GoalCategory>? Goals
    {
        get;
        init;
    }

    public IReadOnlyCollection<Guid>? InterestIds
    {
        get;
        init;
    }

    public IReadOnlyCollection<Guid>? SkillIds
    {
        get;
        init;
    }

    public Guid? FacultyId { get; init; }

    public int? Course { get; init; }

    public int Page { get; init; } = 1;
}

public sealed record UserSearchItem(
    Guid UserId,
    Guid ProfileId,
    string? FirstName,
    string? LastName,
    string? AboutMe,
    Guid? FacultyId,
    int? Course);

public interface IUserSearchQueries
{
    Task<QueryPage<UserSearchItem>> SearchAsync(
        UserSearchFilter filter,
        CancellationToken cancellationToken = default);
}