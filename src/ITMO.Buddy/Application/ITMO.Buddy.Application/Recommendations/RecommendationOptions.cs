namespace ITMO.Buddy.Application.Recommendations;

public sealed class RecommendationOptions
{
    public double GoalsWeight { get; set; } = 0.4;

    public double InterestsWeight { get; set; } = 0.3;

    public double SkillsWeight { get; set; } = 0.2;

    public double AcademicWeight { get; set; } = 0.1;

    public TimeSpan SkipDuration { get; set; } = TimeSpan.FromDays(7);

    internal bool HasValidWeights() =>
        IsValidWeight(GoalsWeight) &&
        IsValidWeight(InterestsWeight) &&
        IsValidWeight(SkillsWeight) &&
        IsValidWeight(AcademicWeight) &&
        Math.Abs(GoalsWeight + InterestsWeight + SkillsWeight + AcademicWeight - 1) <= 1e-9;

    private static bool IsValidWeight(double weight) => double.IsFinite(weight) && weight >= 0;
}
