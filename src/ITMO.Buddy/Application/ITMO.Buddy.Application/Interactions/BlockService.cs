using ITMO.Buddy.Application.Abstractions.Persistence;
using ITMO.Buddy.Application.Abstractions.Services;
using ITMO.Buddy.Application.Abstractions.Time;
using ITMO.Buddy.Application.Common;
using ITMO.Buddy.Application.Errors;
using ITMO.Buddy.Domain.Interactions;

namespace ITMO.Buddy.Application.Interactions;

internal sealed class BlockService(
    CurrentUserGuard currentUser,
    IUserRepository users,
    IUserBlockRepository blocks,
    IUserPairLock pairLock,
    ITransactionRunner transactions,
    IDateTimeProvider clock) : IBlockService
{
    public Task BlockAsync(Guid targetUserId, CancellationToken cancellationToken = default) =>
        ApplicationOperation.RunAsync(async () =>
        {
            var userId = GetActorId(targetUserId);

            try
            {
                await transactions.ExecuteAsync(async token =>
                {
                    await pairLock.AcquireAsync(userId, targetUserId, token);
                    await currentUser.RequireActiveUserAsync(token);

                    if (await blocks.GetDirectedAsync(userId, targetUserId, token) is not null)
                    {
                        return;
                    }

                    // A user can maintain their own block even if the other side has blocked them.
                    if (await users.GetByIdAsync(targetUserId, token) is not { IsActive: true })
                    {
                        throw AppException.NotFound("user_block.target_unavailable", "The target user is unavailable.");
                    }

                    blocks.Add(new UserBlock(Guid.NewGuid(), userId, targetUserId, clock.UtcNow));
                }, cancellationToken);
            }
            catch (PersistenceException error) when (
                error.Code == PersistenceErrorCode.UniqueConstraintViolation &&
                error.ConstraintName == "ux_user_blocks_blocker_blocked")
            {
                // An equivalent directional block already exists.
            }
        });

    public Task UnblockAsync(Guid targetUserId, CancellationToken cancellationToken = default) =>
        ApplicationOperation.RunAsync(async () =>
        {
            var userId = GetActorId(targetUserId);
            await transactions.ExecuteAsync(async token =>
            {
                await pairLock.AcquireAsync(userId, targetUserId, token);
                await currentUser.RequireActiveUserAsync(token);
                var block = await blocks.GetDirectedAsync(userId, targetUserId, token);
                if (block is not null)
                {
                    blocks.Remove(block);
                }
            }, cancellationToken);
        });

    private Guid GetActorId(Guid targetUserId)
    {
        var userId = currentUser.RequireUserId();
        if (targetUserId == Guid.Empty)
        {
            throw AppException.Validation("user_block.target_required", "A target user ID is required.");
        }

        if (targetUserId == userId)
        {
            throw AppException.Validation("user_block.self_block", "You cannot block yourself.");
        }

        return userId;
    }
}
