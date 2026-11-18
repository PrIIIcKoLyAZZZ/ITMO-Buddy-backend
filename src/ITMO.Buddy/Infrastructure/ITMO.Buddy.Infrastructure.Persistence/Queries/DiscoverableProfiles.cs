using ITMO.Buddy.Domain.Enums;
using ITMO.Buddy.Domain.Users;
using ITMO.Buddy.Infrastructure.Persistence.Context;
using Microsoft.EntityFrameworkCore;

namespace ITMO.Buddy.Infrastructure.Persistence.Queries;

internal static class DiscoverableProfiles
{
    public static IQueryable<UserProfile> ForViewer(BuddyDbContext context, Guid viewerId) =>
        context.UserProfiles.AsNoTracking().Where(profile =>
            profile.IsVisible &&
            profile.UserId != viewerId &&
            context.Users.Any(user => user.Id == profile.UserId && user.Status == UserStatus.Active) &&
            !context.UserBlocks.Any(block =>
                (block.BlockerId == viewerId && block.BlockedUserId == profile.UserId) ||
                (block.BlockerId == profile.UserId && block.BlockedUserId == viewerId)));
}
