using ITMO.Buddy.Application.Abstractions.Identity;
using ITMO.Buddy.Application.Abstractions.Persistence;
using ITMO.Buddy.Application.Errors;
using ITMO.Buddy.Domain.Users;

namespace ITMO.Buddy.Application.Common;

internal sealed class CurrentUserGuard(
    ICurrentUser currentUser,
    IUserRepository users)
{
    public Guid RequireUserId()
    {
        if (!currentUser.IsAuthenticated ||
            currentUser.UserId is not { } userId ||
            userId == Guid.Empty)
        {
            throw AppException.Unauthorized("auth.required", "Authentication is required.");
        }

        return userId;
    }

    public async Task<User> RequireActiveUserAsync(CancellationToken cancellationToken)
    {
        var user = await users.GetByIdAsync(RequireUserId(), cancellationToken);
        if (user is null)
        {
            throw AppException.Unauthorized("auth.required", "Authentication is required.");
        }

        if (!user.IsActive)
        {
            throw AppException.Forbidden("user.status.not_active", "The account is not active.");
        }

        return user;
    }
}
