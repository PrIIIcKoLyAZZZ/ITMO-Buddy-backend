using ITMO.Buddy.Application.Abstractions.Persistence;
using ITMO.Buddy.Domain.Users;
using ITMO.Buddy.Infrastructure.Persistence.Context;
using ITMO.Buddy.Infrastructure.Persistence.Models;
using Microsoft.EntityFrameworkCore;

namespace ITMO.Buddy.Infrastructure.Persistence.Transactions;

public sealed class BuddyUnitOfWork : IUnitOfWork
{
    private readonly BuddyDbContext _context;
    private readonly ProfileGoalsStore _goalsStore;

    public BuddyUnitOfWork(
        BuddyDbContext context,
        ProfileGoalsStore goalsStore)
    {
        _context = context;
        _goalsStore = goalsStore;
    }

    public async Task<int> SaveChangesAsync(
        CancellationToken cancellationToken = default)
    {
        try
        {
            _context.ChangeTracker.DetectChanges();

            var changedJoinProfileIds =
                GetChangedJoinProfileIds();

            var profileEntries = _context.ChangeTracker
                .Entries<UserProfile>()
                .Where(x =>
                    x.State != EntityState.Detached &&
                    x.State != EntityState.Deleted)
                .ToArray();

            foreach (var entry in profileEntries)
            {
                var profile = entry.Entity;

                var goalsChanged =
                    await _goalsStore.SynchronizeAsync(
                        profile,
                        cancellationToken);

                var collectionsChanged =
                    goalsChanged ||
                    changedJoinProfileIds.Contains(profile.Id);

                if (collectionsChanged &&
                    entry.State != EntityState.Added)
                {
                    // Force an UPDATE even when the
                    // scalar profile fields are unchanged.
                    //
                    // PostgreSQL xmin then protects
                    // collection-only modifications.

                    entry.Property(x => x.IsVisible)
                        .IsModified = true;
                }
            }

            var result = await _context.SaveChangesAsync(
                cancellationToken);

            _goalsStore.AcceptSavedChanges(
                profileEntries.Select(x => x.Entity));

            return result;
        }
        catch (Exception exception) when (
            PostgresExceptionTranslator.CanTranslate(exception))
        {
            throw PostgresExceptionTranslator.Translate(
                exception);
        }
    }

    private HashSet<Guid> GetChangedJoinProfileIds()
    {
        var result = new HashSet<Guid>();

        foreach (var entry in
                 _context.ChangeTracker.Entries())
        {
            if (entry.State != EntityState.Added &&
                entry.State != EntityState.Deleted)
            {
                continue;
            }

            if (entry.Metadata.Name is not
                ("ProfileInterest" or "ProfileSkill"))
            {
                continue;
            }

            if (entry.Property("profile_id").CurrentValue
                is Guid profileId)
            {
                result.Add(profileId);
            }
        }

        return result;
    }
}