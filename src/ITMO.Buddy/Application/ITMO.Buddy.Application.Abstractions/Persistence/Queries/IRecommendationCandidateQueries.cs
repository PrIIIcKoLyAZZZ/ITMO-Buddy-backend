using ITMO.Buddy.Domain.Enums;

namespace ITMO.Buddy.Application.Abstractions.Persistence.Queries;

public sealed record RecommendationCandidateRequest
{
    public required Guid CurrentUserId { get; init; }

    public GoalCategory? GoalFilter { get; init; }

    /// <summary>Exclusive cursor in ascending profile ID order.</summary>
    public Guid? AfterProfileId { get; init; }

    public int Limit { get; init; } = 200;
}

/// <summary>Scoring inputs, with unique values in each collection.</summary>
public sealed record RecommendationCandidate(
    Guid UserId,
    Guid ProfileId,
    Guid? FacultyId,
    int? Course,
    IReadOnlyList<GoalCategory> Goals,
    IReadOnlyList<Guid> InterestIds,
    IReadOnlyList<Guid> SkillIds);

public interface IRecommendationCandidateQueries
{
    /// <summary>Returns one batch in ascending profile ID order.</summary>
    Task<IReadOnlyList<RecommendationCandidate>>
        GetCandidatesAsync(
            RecommendationCandidateRequest request,
            CancellationToken cancellationToken = default);
}