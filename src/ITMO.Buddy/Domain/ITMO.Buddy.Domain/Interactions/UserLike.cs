using ITMO.Buddy.Domain.Common;

namespace ITMO.Buddy.Domain.Interactions;

public sealed class UserLike : Entity
{
    public Guid SenderId { get; private set; }

    public Guid TargetId { get; private set; }

    public DateTimeOffset CreatedAtUtc { get; private set; }

    private UserLike()
    {
    }

    public UserLike(
        Guid id,
        Guid senderId,
        Guid targetId,
        DateTimeOffset createdAtUtc)
        : base(id)
    {
        if (senderId == Guid.Empty ||
            targetId == Guid.Empty)
        {
            throw new DomainException(
                "user_like.user_id.empty",
                "User identifiers cannot be empty.");
        }

        if (senderId == targetId)
        {
            throw new DomainException(
                "user_like.self_like",
                "User cannot like themselves.");
        }

        SenderId = senderId;
        TargetId = targetId;

        CreatedAtUtc = UtcTime.Require(
            createdAtUtc,
            nameof(createdAtUtc));
    }
}