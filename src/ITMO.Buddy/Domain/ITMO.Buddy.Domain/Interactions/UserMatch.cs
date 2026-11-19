using ITMO.Buddy.Domain.Common;

namespace ITMO.Buddy.Domain.Interactions;

public sealed class UserMatch : Entity
{
    public Guid FirstUserId { get; private set; }

    public Guid SecondUserId { get; private set; }

    public DateTimeOffset CreatedAtUtc { get; private set; }

    private UserMatch()
    {
    }

    public UserMatch(
        Guid id,
        Guid firstUserId,
        Guid secondUserId,
        DateTimeOffset createdAtUtc)
        : base(id)
    {
        if (firstUserId == Guid.Empty ||
            secondUserId == Guid.Empty)
        {
            throw new DomainException(
                "user_match.user_id.empty",
                "User identifiers cannot be empty.");
        }

        if (firstUserId == secondUserId)
        {
            throw new DomainException(
                "user_match.same_user",
                "Match requires two different users.");
        }

        if (firstUserId.CompareTo(secondUserId) < 0)
        {
            FirstUserId = firstUserId;
            SecondUserId = secondUserId;
        }
        else
        {
            FirstUserId = secondUserId;
            SecondUserId = firstUserId;
        }

        CreatedAtUtc = UtcTime.Require(
            createdAtUtc,
            nameof(createdAtUtc));
    }

    public bool ContainsUser(Guid userId)
    {
        return FirstUserId == userId ||
               SecondUserId == userId;
    }

    public bool IsBetween(Guid firstUserId, Guid secondUserId)
    {
        return firstUserId != secondUserId &&
               ContainsUser(firstUserId) &&
               ContainsUser(secondUserId);
    }
}