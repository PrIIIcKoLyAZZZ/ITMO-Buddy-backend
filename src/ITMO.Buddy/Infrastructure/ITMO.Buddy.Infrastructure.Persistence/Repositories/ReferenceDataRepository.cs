using ITMO.Buddy.Application.Abstractions.Persistence;
using ITMO.Buddy.Domain.Users;
using ITMO.Buddy.Infrastructure.Persistence.Context;
using ITMO.Buddy.Infrastructure.Persistence.Queries;
using Microsoft.EntityFrameworkCore;

namespace ITMO.Buddy.Infrastructure.Persistence.Repositories;

public sealed class ReferenceDataRepository
    : IReferenceDataRepository
{
    private const int MaxSearchLimit = 20;

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

    public async Task<IReadOnlyList<Interest>>
        SearchInterestsAsync(
            string? searchText,
            int take,
            CancellationToken cancellationToken = default)
    {
        ValidateSearchLimit(take);

        var text = NormalizeSearchText(searchText);

        var query = _context.Interests
            .AsNoTracking()
            .AsQueryable();

        if (text is not null)
        {
            var pattern = SearchText.ToContainsPattern(text);

            query = query.Where(x =>
                EF.Functions.ILike(
                    x.Name, pattern, "\\"));
        }

        return await query
            .OrderBy(x => x.Name)
            .ThenBy(x => x.Id)
            .Take(take)
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

    public async Task<IReadOnlyList<Skill>>
        SearchSkillsAsync(
            string? searchText,
            int take,
            CancellationToken cancellationToken = default)
    {
        ValidateSearchLimit(take);

        var text = NormalizeSearchText(searchText);

        var query = _context.Skills
            .AsNoTracking()
            .AsQueryable();

        if (text is not null)
        {
            var pattern = SearchText.ToContainsPattern(text);

            query = query.Where(x =>
                EF.Functions.ILike(
                    x.Name, pattern, "\\"));
        }

        return await query
            .OrderBy(x => x.Name)
            .ThenBy(x => x.Id)
            .Take(take)
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

    private static void ValidateSearchLimit(int take)
    {
        if (take < 1 || take > MaxSearchLimit)
        {
            throw new ArgumentOutOfRangeException(
                nameof(take),
                "Search limit must be between 1 and 20.");
        }
    }

    private static string? NormalizeSearchText(
        string? searchText)
    {
        var normalized = searchText?.Trim();

        if (string.IsNullOrWhiteSpace(normalized))
        {
            return null;
        }

        if (normalized.Length > SearchText.MaxLength)
        {
            throw new ArgumentOutOfRangeException(
                nameof(searchText),
                "Search text cannot exceed 200 characters.");
        }

        return normalized;
    }
}