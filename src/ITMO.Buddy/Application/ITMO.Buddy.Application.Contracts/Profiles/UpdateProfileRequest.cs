namespace ITMO.Buddy.Application.Contracts.Profiles;

/// <summary>
/// Полная замена редактируемых данных анкеты.
/// Используется с PUT /api/profiles/me.
/// </summary>
public sealed record UpdateProfileRequest
{
    public required string? FirstName { get; init; }

    public required string? LastName { get; init; }

    public required Guid? FacultyId { get; init; }

    public required int? Course { get; init; }

    public required string? AboutMe { get; init; }

    public required string? Contact { get; init; }

    public required IReadOnlyList<string> Goals
    {
        get;
        init;
    }

    public required IReadOnlyList<Guid> InterestIds
    {
        get;
        init;
    }

    public required IReadOnlyList<Guid> SkillIds
    {
        get;
        init;
    }

    public required bool IsVisible { get; init; }
}