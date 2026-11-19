using ITMO.Buddy.Domain.Enums;

namespace ITMO.Buddy.Infrastructure.Persistence.Models;

internal sealed class ProfileGoalRow
{
    public Guid ProfileId { get; private set; }

    public GoalCategory GoalCategory { get; private set; }

    private ProfileGoalRow()
    {
    }

    public ProfileGoalRow(
        Guid profileId,
        GoalCategory goalCategory)
    {
        if (profileId == Guid.Empty)
        {
            throw new ArgumentException(
                "Profile identifier cannot be empty.",
                nameof(profileId));
        }

        if (!Enum.IsDefined(goalCategory))
        {
            throw new ArgumentOutOfRangeException(
                nameof(goalCategory));
        }

        ProfileId = profileId;
        GoalCategory = goalCategory;
    }
}