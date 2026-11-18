using ITMO.Buddy.Application.Abstractions.Persistence.Queries;
using ITMO.Buddy.Application.Contracts.Recommendations;
using ITMO.Buddy.Domain.Users;
using Microsoft.Extensions.Options;

namespace ITMO.Buddy.Application.Recommendations;

internal sealed class RecommendationScorer(IOptions<RecommendationOptions> options)
{
    public Func<RecommendationCandidate, ScoredCandidate> CompareWith(UserProfile viewer)
    {
        var weights = options.Value;
        var viewerGoals = viewer.Goals.ToHashSet();
        var viewerInterests = viewer.Interests.Select(interest => interest.Id).ToHashSet();
        var viewerSkills = viewer.Skills.Select(skill => skill.Id).ToHashSet();
        var viewerFaculty = viewer.FacultyId;
        var viewerCourse = viewer.Course;

        return candidate =>
        {
            var goals = Jaccard(viewerGoals, candidate.Goals);
            var interests = Jaccard(viewerInterests, candidate.InterestIds);
            var skills = Jaccard(viewerSkills, candidate.SkillIds);
            var sameFaculty = viewerFaculty.HasValue && viewerFaculty == candidate.FacultyId;
            var courseProximity = viewerCourse is { } course && candidate.Course is { } candidateCourse
                ? Math.Max(0, 1 - Math.Abs((long)course - candidateCourse) / 4d)
                : 0;

            var academic = 0.5 * (sameFaculty ? 1 : 0) + 0.5 * courseProximity;
            var score = weights.GoalsWeight * goals + weights.InterestsWeight * interests +
                        weights.SkillsWeight * skills + weights.AcademicWeight * academic;

            return new ScoredCandidate(
                candidate.UserId, Math.Clamp(score, 0, 1), goals > 0, interests > 0,
                skills > 0, sameFaculty, courseProximity > 0);
        };
    }

    private static double Jaccard<T>(IReadOnlySet<T> first, IReadOnlyList<T> second)
    {
        // Candidate queries return unique values for every component.
        var intersection = 0;
        foreach (var value in second)
        {
            if (first.Contains(value))
            {
                intersection++;
            }
        }

        var union = first.Count + second.Count - intersection;
        return union == 0 ? 0 : (double)intersection / union;
    }
}

internal readonly record struct ScoredCandidate(
    Guid UserId,
    double Score,
    bool SharedGoals,
    bool SharedInterests,
    bool SharedSkills,
    bool SameFaculty,
    bool NearbyCourse)
{
    public IReadOnlyList<RecommendationReason> GetReasons()
    {
        var reasons = new List<RecommendationReason>();
        if (SharedGoals)
        {
            reasons.Add(new(RecommendationReasonCodes.SharedGoals, "You share at least one goal."));
        }

        if (SharedInterests)
        {
            reasons.Add(new(RecommendationReasonCodes.SharedInterests, "You share at least one interest."));
        }

        if (SharedSkills)
        {
            reasons.Add(new(RecommendationReasonCodes.SharedSkills, "You share at least one skill."));
        }

        if (SameFaculty)
        {
            reasons.Add(new(RecommendationReasonCodes.SameFaculty, "You study at the same faculty."));
        }

        if (NearbyCourse)
        {
            reasons.Add(new(RecommendationReasonCodes.CourseProximity, "You are in the same or nearby years of study."));
        }

        return reasons;
    }
}
