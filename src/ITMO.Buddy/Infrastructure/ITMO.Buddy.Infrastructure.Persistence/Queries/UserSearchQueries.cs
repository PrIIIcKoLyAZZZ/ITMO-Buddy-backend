using ITMO.Buddy.Application.Abstractions.Persistence.Queries;
using ITMO.Buddy.Domain.Enums;
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

        if (filter.Course is <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(filter),
                "Course must be positive.");
        }

        if (filter.FacultyId == Guid.Empty)
        {
            throw new ArgumentException(
                "Faculty identifier cannot be empty.",
                nameof(filter));
        }

        var currentUserId = filter.CurrentUserId;

        var query = _context.UserProfiles
            .AsNoTracking()
            .Where(profile =>
                profile.IsVisible &&
                profile.UserId != currentUserId &&
                _context.Users.Any(user =>
                    user.Id == profile.UserId &&
                    user.Status == UserStatus.Active) &&
                !_context.UserBlocks.Any(block =>
                    (block.BlockerId == currentUserId &&
                     block.BlockedUserId == profile.UserId) ||
                    (block.BlockerId == profile.UserId &&
                     block.BlockedUserId == currentUserId)));

        // Text search: name, biography, interests, skills.

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

        // OR inside goals.

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

        // OR inside interests.

        if (interestIds.Length > 0)
        {
            query = query.Where(profile =>
                profile.Interests.Any(interest =>
                    interestIds.Contains(interest.Id)));
        }

        // OR inside skills.

        if (skillIds.Length > 0)
        {
            query = query.Where(profile =>
                profile.Skills.Any(skill =>
                    skillIds.Contains(skill.Id)));
        }

        if (filter.FacultyId is Guid facultyId)
        {
            query = query.Where(profile =>
                profile.FacultyId == facultyId);
        }

        if (filter.Course is int course)
        {
            query = query.Where(profile =>
                profile.Course == course);
        }

        var totalCount = await query.CountAsync(
            cancellationToken);

        var items = await query
            .OrderBy(profile => profile.Id)
            .Skip(skip)
            .Take(RepositoryPaging.PageSize)
            .Select(profile => new UserSearchItem(
                profile.UserId,
                profile.Id,
                profile.FirstName,
                profile.LastName,
                profile.AboutMe,
                profile.FacultyId,
                profile.Course))
            .ToListAsync(cancellationToken);

        return new QueryPage<UserSearchItem>(
            items,
            filter.Page,
            RepositoryPaging.PageSize,
            totalCount);
    }
}