using ITMO.Buddy.Application.Abstractions.Persistence.Queries;
using ITMO.Buddy.Domain.Enums;
using ITMO.Buddy.Domain.Users;
using ITMO.Buddy.Infrastructure.Persistence.Context;
using ITMO.Buddy.Infrastructure.Persistence.Models;
using ITMO.Buddy.Infrastructure.Persistence.Repositories;
using Microsoft.EntityFrameworkCore;
using NpgsqlTypes;

namespace ITMO.Buddy.Infrastructure.Persistence.Queries;

public sealed class UserSearchQueries : IUserSearchQueries
{
    private readonly BuddyDbContext _context;

    public UserSearchQueries(BuddyDbContext context)
    {
        _context = context;
    }

    public async Task<QueryPage<UserSearchItem>> SearchAsync(
        UserSearchFilter filter,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(filter);

        if (filter.CurrentUserId == Guid.Empty)
        {
            throw new ArgumentException(
                "Current user identifier is required.",
                nameof(filter));
        }

        var skip = RepositoryPaging.GetSkip(filter.Page);
        var text = SearchText.Normalize(filter.Text);

        var goals = filter.Goals?
            .Distinct()
            .ToArray() ?? Array.Empty<GoalCategory>();

        if (goals.Any(x => !Enum.IsDefined(x)))
        {
            throw new ArgumentException(
                "Unknown goal category.",
                nameof(filter));
        }

        var interestIds = filter.InterestIds?
            .Distinct()
            .ToArray() ?? Array.Empty<Guid>();

        var skillIds = filter.SkillIds?
            .Distinct()
            .ToArray() ?? Array.Empty<Guid>();

        var facultyIds = filter.FacultyIds?
            .Distinct()
            .ToArray() ?? Array.Empty<Guid>();

        if (filter.FacultyId is Guid facultyId)
        {
            facultyIds = facultyIds
                .Append(facultyId)
                .Distinct()
                .ToArray();
        }

        var courses = filter.Courses?
            .Distinct()
            .ToArray() ?? Array.Empty<int>();

        if (filter.Course is int course)
        {
            courses = courses
                .Append(course)
                .Distinct()
                .ToArray();
        }

        if (interestIds.Contains(Guid.Empty) ||
            skillIds.Contains(Guid.Empty) ||
            facultyIds.Contains(Guid.Empty))
        {
            throw new ArgumentException(
                "Filter identifiers cannot be empty GUIDs.",
                nameof(filter));
        }

        if (courses.Any(x => x <= 0))
        {
            throw new ArgumentOutOfRangeException(
                nameof(filter),
                "Courses must contain positive numbers.");
        }

        var query = DiscoverableProfiles.ForViewer(_context, filter.CurrentUserId);

        // Текстовый поиск по имени, описанию,
        // интересам и навыкам.

        if (text is not null)
        {
            var pattern = SearchText.ToContainsPattern(text);
            var useFullTextSearch = text.Length >= 3;

            query = query.Where(profile =>
                (useFullTextSearch &&
                 EF.Property<NpgsqlTsVector>(
                     profile, "SearchVector")
                     .Matches(
                         EF.Functions.PlainToTsQuery(
                             "russian", text))) ||

                EF.Functions.ILike(
                    profile.FirstName!, pattern, "\\") ||

                EF.Functions.ILike(
                    profile.LastName!, pattern, "\\") ||

                EF.Functions.ILike(
                    profile.AboutMe!, pattern, "\\") ||

                profile.Interests.Any(interest =>
                    EF.Functions.ILike(
                        interest.Name, pattern, "\\")) ||

                profile.Skills.Any(skill =>
                    EF.Functions.ILike(
                        skill.Name, pattern, "\\")));
        }

        // OR внутри выбранных целей.

        if (goals.Length > 0)
        {
            var studies = goals.Contains(GoalCategory.Studies);
            var projects = goals.Contains(GoalCategory.Projects);
            var events = goals.Contains(GoalCategory.Events);
            var interests = goals.Contains(GoalCategory.Interests);

            query = query.Where(profile =>
                _context.Set<ProfileGoalRow>().Any(goal =>
                    goal.ProfileId == profile.Id &&
                    ((studies &&
                      goal.GoalCategory == GoalCategory.Studies) ||
                     (projects &&
                      goal.GoalCategory == GoalCategory.Projects) ||
                     (events &&
                      goal.GoalCategory == GoalCategory.Events) ||
                     (interests &&
                      goal.GoalCategory == GoalCategory.Interests))));
        }

        // OR внутри интересов.

        if (interestIds.Length > 0)
        {
            query = query.Where(profile =>
                profile.Interests.Any(interest =>
                    interestIds.Contains(interest.Id)));
        }

        // OR внутри навыков.

        if (skillIds.Length > 0)
        {
            query = query.Where(profile =>
                profile.Skills.Any(skill =>
                    skillIds.Contains(skill.Id)));
        }

        // OR внутри факультетов.

        if (facultyIds.Length > 0)
        {
            query = query.Where(profile =>
                profile.FacultyId.HasValue &&
                facultyIds.Contains(profile.FacultyId.Value));
        }

        // OR внутри курсов.

        if (courses.Length > 0)
        {
            query = query.Where(profile =>
                profile.Course.HasValue &&
                courses.Contains(profile.Course.Value));
        }

        var totalCount = await query.CountAsync(
            cancellationToken);

        var items = await LoadCardsAsync(query
            .OrderBy(profile => profile.Id)
            .Skip(skip)
            .Take(RepositoryPaging.PageSize), cancellationToken);

        return new QueryPage<UserSearchItem>(
            items,
            filter.Page,
            RepositoryPaging.PageSize,
            totalCount);
    }

    public Task<IReadOnlyList<UserSearchItem>> GetCardsAsync(
        Guid currentUserId,
        IReadOnlyCollection<Guid> userIds,
        CancellationToken cancellationToken = default) =>
        LoadCardsAsync(DiscoverableProfiles.ForViewer(_context, currentUserId)
            .Where(profile => userIds.Contains(profile.UserId)), cancellationToken);

    private async Task<IReadOnlyList<UserSearchItem>> LoadCardsAsync(
        IQueryable<UserProfile> query,
        CancellationToken cancellationToken)
    {
        var items = await query
            .Select(profile => new UserSearchItem(
                profile.UserId,
                profile.Id,
                profile.FirstName,
                profile.LastName,
                profile.AboutMe,
                profile.FacultyId,
                profile.Course)
            {
                PhotoObjectKey = profile.PhotoObjectKey,
                FacultyName = _context.Faculties
                    .Where(faculty => faculty.Id == profile.FacultyId)
                    .Select(faculty => faculty.Name)
                    .FirstOrDefault()
            })
            .ToArrayAsync(cancellationToken);

        if (items.Length == 0)
        {
            return items;
        }

        var profileIds = items.Select(item => item.ProfileId).ToArray();
        var goalRows = await _context.Set<ProfileGoalRow>()
            .AsNoTracking()
            .Where(goal => profileIds.Contains(goal.ProfileId))
            .OrderBy(goal => goal.GoalCategory)
            .Select(goal => new { goal.ProfileId, goal.GoalCategory })
            .ToArrayAsync(cancellationToken);

        var interestRows = await _context.UserProfiles
            .AsNoTracking()
            .Where(profile => profileIds.Contains(profile.Id))
            .SelectMany(profile => profile.Interests
                .OrderBy(interest => interest.NormalizedName)
                .ThenBy(interest => interest.Id)
                .Take(5)
                .Select(interest => new
                {
                    ProfileId = profile.Id,
                    interest.Id,
                    interest.Name,
                    interest.NormalizedName
                }))
            .OrderBy(interest => interest.NormalizedName)
            .ThenBy(interest => interest.Id)
            .ToArrayAsync(cancellationToken);

        var goals = goalRows.ToLookup(row => row.ProfileId, row => row.GoalCategory);
        var interests = interestRows.ToLookup(row => row.ProfileId, row => new UserSearchInterest(row.Id, row.Name));
        return items.Select(item => item with
        {
            Goals = goals[item.ProfileId].ToArray(),
            Interests = interests[item.ProfileId].ToArray()
        }).ToArray();
    }
}
