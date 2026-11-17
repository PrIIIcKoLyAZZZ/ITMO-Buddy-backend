using ITMO.Buddy.Domain.Enums;
using ITMO.Buddy.Domain.Users;
using ITMO.Buddy.Infrastructure.Persistence.Context;
using Microsoft.EntityFrameworkCore;

namespace ITMO.Buddy.Infrastructure.Persistence.Models;

public sealed class ProfileGoalsStore
{
    private readonly BuddyDbContext _context;

    private readonly Dictionary<Guid, HashSet<GoalCategory>>
        _snapshots = new();

    public ProfileGoalsStore(BuddyDbContext context)
    {
        _context = context;
    }

    public async Task LoadAsync(
        UserProfile profile,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(profile);

        if (_snapshots.ContainsKey(profile.Id))
        {
            return;
        }

        var goals = await _context
            .Set<ProfileGoalRow>()
            .AsNoTracking()
            .Where(x => x.ProfileId == profile.Id)
            .OrderBy(x => x.GoalCategory)
            .Select(x => x.GoalCategory)
            .ToListAsync(cancellationToken);

        profile.SetGoals(goals);

        _snapshots.Add(
            profile.Id,
            goals.ToHashSet());
    }

    public void RegisterNew(UserProfile profile)
    {
        ArgumentNullException.ThrowIfNull(profile);

        if (!_snapshots.TryAdd(
            profile.Id,
            new HashSet<GoalCategory>()))
        {
            throw new InvalidOperationException(
                "Profile goals are already registered.");
        }
    }

    public async Task<bool> SynchronizeAsync(
        UserProfile profile,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(profile);

        if (!_snapshots.TryGetValue(
            profile.Id,
            out var originalGoals))
        {
            throw new InvalidOperationException(
                "Profile goals were not loaded. " +
                "Load the profile through UserProfileRepository.");
        }

        var desiredGoals = profile.Goals.ToHashSet();

        if (originalGoals.SetEquals(desiredGoals))
        {
            return false;
        }

        var removedGoals = originalGoals
            .Except(desiredGoals)
            .ToHashSet();

        var addedGoals = desiredGoals
            .Except(originalGoals)
            .ToArray();

        if (removedGoals.Count > 0)
        {
            var existingRows = await _context
                .Set<ProfileGoalRow>()
                .Where(x => x.ProfileId == profile.Id)
                .ToListAsync(cancellationToken);

            foreach (var row in existingRows)
            {
                if (removedGoals.Contains(row.GoalCategory))
                {
                    _context.Remove(row);
                }
            }
        }

        foreach (var goal in addedGoals)
        {
            _context.Add(new ProfileGoalRow(
                profile.Id,
                goal));
        }

        return true;
    }

    public void AcceptSavedChanges(
        IEnumerable<UserProfile> profiles)
    {
        ArgumentNullException.ThrowIfNull(profiles);

        foreach (var profile in profiles)
        {
            if (_snapshots.ContainsKey(profile.Id))
            {
                _snapshots[profile.Id] =
                    profile.Goals.ToHashSet();
            }
        }
    }
}