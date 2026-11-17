using ITMO.Buddy.Domain.Common;

namespace ITMO.Buddy.Domain.Interactions;

public sealed class UserBlock : Entity
{
    public Guid BlockerId { get; private set; }

    public Guid BlockedUserId { get; private set; }

    public DateTimeOffset CreatedAtUtc { get; private set; }

    private UserBlock()
    {
    }

    public UserBlock(
        Guid id,
        Guid blockerId,
        Guid blockedUserId,
        DateTimeOffset createdAtUtc)
        : base(id)
    {
        if (blockerId == Guid.Empty ||
            blockedUserId == Guid.Empty)
        {
            throw new DomainException(
                "user_block.user_id.empty",
                "User identifiers cannot be empty.");
        }

        if (blockerId == blockedUserId)
        {
            throw new DomainException(
                "user_block.self_block",
                "User cannot block themselves.");
        }

        BlockerId = blockerId;
        BlockedUserId = blockedUserId;

        CreatedAtUtc = UtcTime.Require(
            createdAtUtc,
            nameof(createdAtUtc));
    }

    public bool Blocks(Guid blockerId, Guid targetId)
    {
        return BlockerId == blockerId &&
               BlockedUserId == targetId;
    }
}