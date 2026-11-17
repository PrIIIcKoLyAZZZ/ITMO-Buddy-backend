using System.Net.Mail;
using ITMO.Buddy.Domain.Common;
using ITMO.Buddy.Domain.Enums;

namespace ITMO.Buddy.Domain.Users;

public sealed class User : Entity
{
    public string Email { get; private set; } = null!;

    public string PasswordHash { get; private set; } = null!;

    public UserRole Role { get; private set; }

    public UserStatus Status { get; private set; }

    public DateTimeOffset CreatedAtUtc { get; private set; }

    public DateTimeOffset LastActivityAtUtc { get; private set; }

    public DateTimeOffset PersonalDataConsentAtUtc
    {
        get;
        private set;
    }

    public bool IsActive => Status == UserStatus.Active;

    private User()
    {
    }

    public User(
        Guid id,
        string email,
        string passwordHash,
        DateTimeOffset createdAtUtc,
        DateTimeOffset personalDataConsentAtUtc)
        : base(id)
    {
        Email = NormalizeEmail(email);

        ValidatePasswordHash(passwordHash);

        CreatedAtUtc = UtcTime.Require(
            createdAtUtc,
            nameof(createdAtUtc));

        PersonalDataConsentAtUtc = UtcTime.Require(
            personalDataConsentAtUtc,
            nameof(personalDataConsentAtUtc));

        if (PersonalDataConsentAtUtc > CreatedAtUtc)
        {
            throw new DomainException(
                "user.consent.invalid_time",
                "Personal data consent cannot be later than registration.");
        }

        PasswordHash = passwordHash;

        Role = UserRole.Student;
        Status = UserStatus.Active;

        LastActivityAtUtc = CreatedAtUtc;
    }

    public void UpdatePasswordHash(string passwordHash)
    {
        EnsureActive();

        ValidatePasswordHash(passwordHash);

        PasswordHash = passwordHash;
    }

    public void UpdateLastActivity(
        DateTimeOffset activityAtUtc)
    {
        EnsureActive();

        var timestamp = UtcTime.Require(
            activityAtUtc,
            nameof(activityAtUtc));

        if (timestamp > LastActivityAtUtc)
        {
            LastActivityAtUtc = timestamp;
        }
    }

    public void Suspend()
    {
        if (Status != UserStatus.Active)
        {
            throw new DomainException(
                "user.status.invalid_transition",
                "Only an active user can be suspended.");
        }

        Status = UserStatus.Suspended;
    }

    public void Reactivate()
    {
        if (Status != UserStatus.Suspended)
        {
            throw new DomainException(
                "user.status.invalid_transition",
                "Only a suspended user can be reactivated.");
        }

        Status = UserStatus.Active;
    }

    public void Deactivate()
    {
        if (Status == UserStatus.Deactivated)
        {
            throw new DomainException(
                "user.status.invalid_transition",
                "User is already deactivated.");
        }

        Status = UserStatus.Deactivated;
    }

    private void EnsureActive()
    {
        if (!IsActive)
        {
            throw new DomainException(
                "user.status.not_active",
                "Operation requires an active user.");
        }
    }

    private static void ValidatePasswordHash(
        string passwordHash)
    {
        if (string.IsNullOrWhiteSpace(passwordHash))
        {
            throw new DomainException(
                "user.password_hash.empty",
                "Password hash cannot be empty.");
        }
    }

    private static string NormalizeEmail(string email)
    {
        var normalized = email?.Trim().ToLowerInvariant();

        if (string.IsNullOrWhiteSpace(normalized))
        {
            throw new DomainException(
                "user.email.invalid",
                "Email cannot be empty.");
        }

        if (normalized.Any(char.IsWhiteSpace) ||
            !MailAddress.TryCreate(normalized, out var address) ||
            address is null ||
            !string.Equals(
                address.Address,
                normalized,
                StringComparison.OrdinalIgnoreCase) ||
            !address.Host.Contains('.'))
        {
            throw new DomainException(
                "user.email.invalid",
                "Email has an invalid format.");
        }

        return normalized;
    }
}