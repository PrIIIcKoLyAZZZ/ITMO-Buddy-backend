using ITMO.Buddy.Application.Abstractions.Persistence;
using ITMO.Buddy.Domain.Users;
using ITMO.Buddy.Infrastructure.Persistence.Context;
using ITMO.Buddy.Infrastructure.Persistence.Models;
using Microsoft.EntityFrameworkCore;

namespace ITMO.Buddy.Infrastructure.Persistence.Repositories;

public sealed class UserProfileRepository
    : IUserProfileRepository
{
    private readonly BuddyDbContext _context;
    private readonly ProfileGoalsStore _goalsStore;

    public UserProfileRepository(
        BuddyDbContext context,
        ProfileGoalsStore goalsStore)
    {
        _context = context;
        _goalsStore = goalsStore;
    }

    public async Task<UserProfile?> GetByIdAsync(
        Guid profileId,
        CancellationToken cancellationToken = default)
    {
        var profile = await CreateTrackedQuery()
            .SingleOrDefaultAsync(
                x => x.Id == profileId,
                cancellationToken);

        return await LoadGoalsAsync(
            profile,
            cancellationToken);
    }

    public async Task<UserProfile?> GetByUserIdAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        var profile = await CreateTrackedQuery()
            .SingleOrDefaultAsync(
                x => x.UserId == userId,
                cancellationToken);

        return await LoadGoalsAsync(
            profile,
            cancellationToken);
    }

    public void Add(UserProfile profile)
    {
        ArgumentNullException.ThrowIfNull(profile);

        foreach (var interest in profile.Interests)
        {
            if (_context.Entry(interest).State ==
                EntityState.Detached)
            {
                _context.Interests.Attach(interest);
            }
        }

        foreach (var skill in profile.Skills)
        {
            if (_context.Entry(skill).State ==
                EntityState.Detached)
            {
                _context.Skills.Attach(skill);
            }
        }

        _context.UserProfiles.Add(profile);

        // New profiles start with an empty persisted
        // collection, even if Domain already contains goals.
        _goalsStore.RegisterNew(profile);
    }

    public void Remove(UserProfile profile)
    {
        ArgumentNullException.ThrowIfNull(profile);

        _context.UserProfiles.Remove(profile);
    }

    private IQueryable<UserProfile> CreateTrackedQuery()
    {
        return _context.UserProfiles
            .Include(x => x.Interests)
            .Include(x => x.Skills)
            .AsSplitQuery();
    }

    private async Task<UserProfile?> LoadGoalsAsync(
        UserProfile? profile,
        CancellationToken cancellationToken)
    {
        if (profile is null)
        {
            return null;
        }

        await _goalsStore.LoadAsync(
            profile,
            cancellationToken);

        return profile;
    }
}