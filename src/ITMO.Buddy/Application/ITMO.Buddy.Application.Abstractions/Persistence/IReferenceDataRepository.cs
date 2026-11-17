using ITMO.Buddy.Domain.Users;

namespace ITMO.Buddy.Application.Abstractions.Persistence;

public interface IReferenceDataRepository
{
    Task<Faculty?> GetFacultyByIdAsync(
        Guid facultyId,
        CancellationToken cancellationToken = default);

    Task<Faculty?> GetFacultyByNormalizedNameAsync(
        string normalizedName,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Faculty>> GetFacultiesAsync(
        CancellationToken cancellationToken = default);

    void Add(Faculty faculty);

    Task<Interest?> GetInterestByIdAsync(
        Guid interestId,
        CancellationToken cancellationToken = default);

    Task<Interest?> GetInterestByNormalizedNameAsync(
        string normalizedName,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Interest>> GetInterestsByIdsAsync(
        IReadOnlyCollection<Guid> interestIds,
        CancellationToken cancellationToken = default);

    void Add(Interest interest);

    Task<Skill?> GetSkillByIdAsync(
        Guid skillId,
        CancellationToken cancellationToken = default);

    Task<Skill?> GetSkillByNormalizedNameAsync(
        string normalizedName,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Skill>> GetSkillsByIdsAsync(
        IReadOnlyCollection<Guid> skillIds,
        CancellationToken cancellationToken = default);

    void Add(Skill skill);
}