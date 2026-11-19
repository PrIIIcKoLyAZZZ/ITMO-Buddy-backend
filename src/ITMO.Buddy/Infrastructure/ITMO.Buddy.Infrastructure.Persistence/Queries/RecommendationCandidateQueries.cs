using ITMO.Buddy.Application.Abstractions.Persistence.Queries;
using ITMO.Buddy.Domain.Enums;
using ITMO.Buddy.Infrastructure.Persistence.Context;
using ITMO.Buddy.Infrastructure.Persistence.Models;
using Microsoft.EntityFrameworkCore;

namespace ITMO.Buddy.Infrastructure.Persistence.Queries;

public sealed class RecommendationCandidateQueries
    : IRecommendationCandidateQueries
{
    private const int MaxCandidateLimit = 500;

    private readonly BuddyDbContext _context;

    public RecommendationCandidateQueries(
        BuddyDbContext context)
    {
        _context = context;
    }

    public async Task<IReadOnlyList<RecommendationCandidate>>
        GetCandidatesAsync(
            RecommendationCandidateRequest request,
            CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (request.CurrentUserId == Guid.Empty)
        {
            throw new ArgumentException(
                "Current user identifier is required.",
                nameof(request));
        }

        if (request.Limit < 1 ||
            request.Limit > MaxCandidateLimit)
        {
            throw new ArgumentOutOfRangeException(
                nameof(request),
                $"Limit must be between 1 and {MaxCandidateLimit}.");
        }

        if (request.GoalFilter is { } selected &&
            !Enum.IsDefined(selected))
        {
            throw new ArgumentException(
                "Unknown goal category.",
                nameof(request));
        }

        var currentUserId = request.CurrentUserId;

        var query = DiscoverableProfiles.ForViewer(_context, currentUserId)
            .Where(profile => !_context.UserLikes.Any(like =>
                like.SenderId == currentUserId && like.TargetId == profile.UserId));

        if (request.AfterProfileId is { } afterProfileId)
        {
            query = query.Where(profile => EF.Functions.GreaterThan(
                ValueTuple.Create(profile.Id),
                ValueTuple.Create(afterProfileId)));
        }

        if (request.GoalFilter is GoalCategory goalFilter)
        {
            query = query.Where(profile =>
                _context.Set<ProfileGoalRow>().Any(goal =>
                    goal.ProfileId == profile.Id &&
                    goal.GoalCategory == goalFilter));
        }

        // First retrieve a bounded set of profile headers.

        var profiles = await query
            .OrderBy(x => x.Id)
            .Take(request.Limit)
            .Select(x => new
            {
                ProfileId = x.Id,
                x.UserId,
                x.FacultyId,
                x.Course
            })
            .ToListAsync(cancellationToken);

        if (profiles.Count == 0)
        {
            return Array.Empty<RecommendationCandidate>();
        }

        var profileIds = profiles
            .Select(x => x.ProfileId)
            .ToArray();

        // Retrieve goals in one query.

        var goalRows = await _context
            .Set<ProfileGoalRow>()
            .AsNoTracking()
            .Where(x => profileIds.Contains(x.ProfileId))
            .Select(x => new
            {
                x.ProfileId,
                x.GoalCategory
            })
            .ToListAsync(cancellationToken);

        // Retrieve interest identifiers in one query.

        var interestRows = await _context.UserProfiles
            .AsNoTracking()
            .Where(x => profileIds.Contains(x.Id))
            .SelectMany(
                profile => profile.Interests.Select(
                    interest => new
                    {
                        ProfileId = profile.Id,
                        InterestId = interest.Id
                    }))
            .ToListAsync(cancellationToken);

        // Retrieve skill identifiers in one query.

        var skillRows = await _context.UserProfiles
            .AsNoTracking()
            .Where(x => profileIds.Contains(x.Id))
            .SelectMany(
                profile => profile.Skills.Select(
                    skill => new
                    {
                        ProfileId = profile.Id,
                        SkillId = skill.Id
                    }))
            .ToListAsync(cancellationToken);

        var goalsByProfile = goalRows
            .GroupBy(x => x.ProfileId)
            .ToDictionary(
                x => x.Key,
                x => x.Select(y => y.GoalCategory)
                    .Distinct()
                    .ToArray());

        var interestsByProfile = interestRows
            .GroupBy(x => x.ProfileId)
            .ToDictionary(
                x => x.Key,
                x => x.Select(y => y.InterestId)
                    .Distinct()
                    .ToArray());

        var skillsByProfile = skillRows
            .GroupBy(x => x.ProfileId)
            .ToDictionary(
                x => x.Key,
                x => x.Select(y => y.SkillId)
                    .Distinct()
                    .ToArray());

        return profiles
            .Select(profile => new RecommendationCandidate(
                profile.UserId,
                profile.ProfileId,
                profile.FacultyId,
                profile.Course,
                goalsByProfile.GetValueOrDefault(profile.ProfileId)
                    ?? Array.Empty<GoalCategory>(),
                interestsByProfile.GetValueOrDefault(profile.ProfileId)
                    ?? Array.Empty<Guid>(),
                skillsByProfile.GetValueOrDefault(profile.ProfileId)
                    ?? Array.Empty<Guid>()))
            .ToArray();
    }
}