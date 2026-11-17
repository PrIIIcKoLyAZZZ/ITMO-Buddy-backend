using ITMO.Buddy.Domain.Enums;

namespace ITMO.Buddy.Application.Abstractions.Persistence.Queries;

/// <summary>
/// Внутренний фильтр поиска пользователей.
/// AND между группами; OR внутри каждой группы.
/// </summary>
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

    /// <summary>
    /// Множественный выбор факультетов.
    /// </summary>
    public IReadOnlyCollection<Guid>? FacultyIds
    {
        get;
        init;
    }

    /// <summary>
    /// Множественный выбор курсов.
    /// </summary>
    public IReadOnlyCollection<int>? Courses
    {
        get;
        init;
    }

    /// <summary>
    /// Сохранено для обратной совместимости с IB-002.
    /// При совместном использовании с FacultyIds
    /// значения объединяются через OR.
    /// </summary>
    public Guid? FacultyId { get; init; }

    /// <summary>
    /// Сохранено для обратной совместимости с IB-002.
    /// При совместном использовании с Courses
    /// значения объединяются через OR.
    /// </summary>
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