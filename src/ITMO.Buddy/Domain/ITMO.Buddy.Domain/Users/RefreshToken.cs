using ITMO.Buddy.Domain.Common;

namespace ITMO.Buddy.Domain.Users;

public sealed class RefreshToken : Entity
{
    public Guid UserId { get; private set; }

    public string TokenHash { get; private set; } = null!;

    public DateTimeOffset CreatedAtUtc { get; private set; }

    public DateTimeOffset ExpiresAtUtc { get; private set; }

    public DateTimeOffset? RevokedAtUtc { get; private set; }

    public bool IsRevoked => RevokedAtUtc.HasValue;

    private RefreshToken()
    {
    }

    public RefreshToken(
        Guid id,
        Guid userId,
        string tokenHash,
        DateTimeOffset createdAtUtc,
        DateTimeOffset expiresAtUtc)
        : base(id)
    {
        if (userId == Guid.Empty)
        {
            throw new DomainException(
                "refresh_token.user_id.empty",
                "User identifier cannot be empty.");
        }

        if (string.IsNullOrWhiteSpace(tokenHash))
        {
            throw new DomainException(
                "refresh_token.hash.empty",
                "Token hash cannot be empty.");
        }

        CreatedAtUtc = UtcTime.Require(
            createdAtUtc,
            nameof(createdAtUtc));

        ExpiresAtUtc = UtcTime.Require(
            expiresAtUtc,
            nameof(expiresAtUtc));

        if (ExpiresAtUtc <= CreatedAtUtc)
        {
            throw new DomainException(
                "refresh_token.expiration.invalid",
                "Token expiration must be later than creation.");
        }

        UserId = userId;
        TokenHash = tokenHash;
    }

    public bool IsExpired(DateTimeOffset nowUtc)
    {
        var timestamp = UtcTime.Require(
            nowUtc,
            nameof(nowUtc));

        return timestamp >= ExpiresAtUtc;
    }

    public bool IsValidAt(DateTimeOffset nowUtc)
    {
        var timestamp = UtcTime.Require(
            nowUtc,
            nameof(nowUtc));

        return !IsRevoked &&
               timestamp >= CreatedAtUtc &&
               timestamp < ExpiresAtUtc;
    }

    public void Revoke(DateTimeOffset revokedAtUtc)
    {
        var timestamp = UtcTime.Require(
            revokedAtUtc,
            nameof(revokedAtUtc));

        if (IsRevoked)
        {
            return;
        }

        if (timestamp < CreatedAtUtc)
        {
            throw new DomainException(
                "refresh_token.revocation.invalid_time",
                "Token cannot be revoked before creation.");
        }

        RevokedAtUtc = timestamp;
    }
}