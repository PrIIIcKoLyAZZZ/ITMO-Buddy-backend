using ITMO.Buddy.Application.Abstractions.Persistence;
using ITMO.Buddy.Domain.Users;
using ITMO.Buddy.Infrastructure.Persistence.Context;
using Microsoft.EntityFrameworkCore;

namespace ITMO.Buddy.Infrastructure.Persistence.Repositories;

public sealed class ReferenceDataRepository
    : IReferenceDataRepository
{
    private readonly BuddyDbContext _context;

    public ReferenceDataRepository(BuddyDbContext context)
    {
        _context = context;
    }

    public Task<Faculty?> GetFacultyByIdAsync(
        Guid facultyId,
        CancellationToken cancellationToken = default)
    {
        return _context.Faculties
            .FirstOrDefaultAsync(
                x => x.Id == facultyId,
                cancellationToken);
    }

    public Task<Faculty?> GetFacultyByNormalizedNameAsync(
        string normalizedName,
        CancellationToken cancellationToken = default)
    {
        ValidateName(normalizedName);

        return _context.Faculties
            .FirstOrDefaultAsync(
                x => x.NormalizedName == normalizedName,
                cancellationToken);
    }

    public async Task<IReadOnlyList<Faculty>> GetFacultiesAsync(
        CancellationToken cancellationToken = default)
    {
        return await _context.Faculties
            .AsNoTracking()
            .OrderBy(x => x.Name)
            .ThenBy(x => x.Id)
            .ToListAsync(cancellationToken);
    }

    public void Add(Faculty faculty)
    {
        ArgumentNullException.ThrowIfNull(faculty);

        _context.Faculties.Add(faculty);
    }

    public Task<Interest?> GetInterestByIdAsync(
        Guid interestId,
        CancellationToken cancellationToken = default)
    {
        return _context.Interests
            .FirstOrDefaultAsync(
                x => x.Id == interestId,
                cancellationToken);
    }

    public Task<Interest?> GetInterestByNormalizedNameAsync(
        string normalizedName,
        CancellationToken cancellationToken = default)
    {
        ValidateName(normalizedName);

        return _context.Interests
            .FirstOrDefaultAsync(
                x => x.NormalizedName == normalizedName,
                cancellationToken);
    }

    public async Task<IReadOnlyList<Interest>> GetInterestsByIdsAsync(
        IReadOnlyCollection<Guid> interestIds,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(interestIds);

        var ids = interestIds.Distinct().ToArray();

        if (ids.Length == 0)
        {
            return Array.Empty<Interest>();
        }

        return await _context.Interests
            .Where(x => ids.Contains(x.Id))
            .OrderBy(x => x.NormalizedName)
            .ThenBy(x => x.Id)
            .ToListAsync(cancellationToken);
    }

    public void Add(Interest interest)
    {
        ArgumentNullException.ThrowIfNull(interest);

        _context.Interests.Add(interest);
    }

    public Task<Skill?> GetSkillByIdAsync(
        Guid skillId,
        CancellationToken cancellationToken = default)
    {
        return _context.Skills
            .FirstOrDefaultAsync(
                x => x.Id == skillId,
                cancellationToken);
    }

    public Task<Skill?> GetSkillByNormalizedNameAsync(
        string normalizedName,
        CancellationToken cancellationToken = default)
    {
        ValidateName(normalizedName);

        return _context.Skills
            .FirstOrDefaultAsync(
                x => x.NormalizedName == normalizedName,
                cancellationToken);
    }

    public async Task<IReadOnlyList<Skill>> GetSkillsByIdsAsync(
        IReadOnlyCollection<Guid> skillIds,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(skillIds);

        var ids = skillIds.Distinct().ToArray();

        if (ids.Length == 0)
        {
            return Array.Empty<Skill>();
        }

        return await _context.Skills
            .Where(x => ids.Contains(x.Id))
            .OrderBy(x => x.NormalizedName)
            .ThenBy(x => x.Id)
            .ToListAsync(cancellationToken);
    }

    public void Add(Skill skill)
    {
        ArgumentNullException.ThrowIfNull(skill);

        _context.Skills.Add(skill);
    }

    private static void ValidateName(string normalizedName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(
            normalizedName);
    }
}