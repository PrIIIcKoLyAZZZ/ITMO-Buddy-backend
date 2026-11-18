using FluentValidation;
using ITMO.Buddy.Application.Abstractions.Auth;
using ITMO.Buddy.Application.Abstractions.Persistence;
using ITMO.Buddy.Application.Abstractions.Security;
using ITMO.Buddy.Application.Abstractions.Services;
using ITMO.Buddy.Application.Abstractions.Time;
using ITMO.Buddy.Application.Contracts.Auth;
using ITMO.Buddy.Application.Contracts.Common;
using ITMO.Buddy.Application.Errors;
using ITMO.Buddy.Application.Validation;
using ITMO.Buddy.Domain.Users;

namespace ITMO.Buddy.Application.Auth;

internal sealed class AuthService(
    IUserRepository users,
    IAccountMaintenanceRepository accounts,
    IUserProfileRepository profiles,
    IRefreshTokenRepository refreshTokens,
    ITransactionRunner transactions,
    IPasswordHasher passwords,
    ITokenService tokens,
    IDateTimeProvider clock,
    IValidator<RegisterRequest> registrationValidator,
    IValidator<LoginRequest> loginValidator) : IAuthService
{
    private static readonly TimeSpan AccessLifetime = TimeSpan.FromMinutes(30);
    private static readonly TimeSpan RefreshLifetime = TimeSpan.FromDays(14);

    public Task<AuthSessionResult> RegisterAsync(
        RegisterRequest request,
        CancellationToken cancellationToken = default) =>
        ApplicationOperation.RunAsync(async () =>
        {
            await registrationValidator.ValidateRequestAsync(request, cancellationToken);

            var email = NormalizeEmail(request.Email);
            if (await users.ExistsByEmailAsync(email, cancellationToken))
            {
                throw EmailTaken();
            }

            var hash = passwords.Hash(request.Password);

            try
            {
                return await transactions.ExecuteAsync(_ =>
                {
                    var now = clock.UtcNow;
                    var user = new User(Guid.NewGuid(), email, hash, now, now);
                    users.Add(user);
                    profiles.Add(new UserProfile(Guid.NewGuid(), user.Id));
                    return Task.FromResult(IssueSession(user, now));
                }, cancellationToken);
            }
            catch (PersistenceException error) when (
                error.Code == PersistenceErrorCode.UniqueConstraintViolation &&
                error.ConstraintName == "ux_users_email")
            {
                throw EmailTaken();
            }
        });

    public Task<AuthSessionResult> LoginAsync(
        LoginRequest request,
        CancellationToken cancellationToken = default) =>
        ApplicationOperation.RunAsync(async () =>
        {
            try
            {
                await loginValidator.ValidateRequestAsync(request, cancellationToken);
            }
            catch (AppException error) when (error.Error.Category == ErrorCategories.Validation)
            {
                throw InvalidCredentials();
            }

            var user = await users.GetByEmailAsync(NormalizeEmail(request.Email), cancellationToken);
            if (user is null || !passwords.Verify(request.Password, user.PasswordHash) || !user.IsActive)
            {
                throw InvalidCredentials();
            }

            return await transactions.ExecuteAsync(async token =>
            {
                if (!await accounts.LockAsync(user.Id, token))
                {
                    throw InvalidCredentials();
                }

                var current = await users.GetByIdAsync(user.Id, token);
                if (current is not { IsActive: true } || current.PasswordHash != user.PasswordHash)
                {
                    throw InvalidCredentials();
                }

                var nowUtc = clock.UtcNow;
                current.UpdateLastActivity(nowUtc);
                return IssueSession(current, nowUtc);
            }, cancellationToken);
        });

    public Task<AuthSessionResult> RefreshAsync(
        string refreshToken,
        CancellationToken cancellationToken = default) =>
        ApplicationOperation.RunAsync(async () =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (string.IsNullOrWhiteSpace(refreshToken))
            {
                throw InvalidSession();
            }

            var hash = tokens.HashRefreshToken(refreshToken);
            try
            {
                return await transactions.ExecuteAsync(async token =>
                {
                    var existing = await refreshTokens.GetByHashAsync(hash, token);
                    var user = existing is null ? null : await users.GetByIdAsync(existing.UserId, token);
                    var now = clock.UtcNow;

                    if (existing is null || user is not { IsActive: true } || !existing.IsValidAt(now))
                    {
                        throw InvalidSession();
                    }

                    existing.Revoke(now);
                    return IssueSession(user, now);
                }, cancellationToken);
            }
            catch (PersistenceException error) when (error.Code == PersistenceErrorCode.ConcurrencyConflict)
            {
                throw InvalidSession();
            }
        });

    public Task LogoutAsync(string? refreshToken, CancellationToken cancellationToken = default) =>
        ApplicationOperation.RunAsync(async () =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (string.IsNullOrWhiteSpace(refreshToken))
            {
                return;
            }

            var hash = tokens.HashRefreshToken(refreshToken);
            try
            {
                await transactions.ExecuteAsync(async token =>
                {
                    var existing = await refreshTokens.GetByHashAsync(hash, token);
                    var now = clock.UtcNow;
                    if (existing is not null && existing.IsValidAt(now))
                    {
                        existing.Revoke(now);
                    }
                }, cancellationToken);
            }
            catch (PersistenceException error) when (error.Code == PersistenceErrorCode.ConcurrencyConflict)
            {
                // The token was revoked or physically deleted by a competing operation.
            }
        });

    private AuthSessionResult IssueSession(User user, DateTimeOffset now)
    {
        var access = tokens.CreateAccessToken(user.Id, user.Role, now);
        var refresh = tokens.CreateRefreshToken(now);
        if (string.IsNullOrWhiteSpace(access.Token) || access.ExpiresAtUtc.Offset != TimeSpan.Zero ||
            access.ExpiresAtUtc <= now || access.ExpiresAtUtc - now > AccessLifetime ||
            string.IsNullOrWhiteSpace(refresh.Value) || refresh.ExpiresAtUtc.Offset != TimeSpan.Zero ||
            refresh.ExpiresAtUtc - now != RefreshLifetime)
        {
            throw new InvalidOperationException("The token provider returned an invalid session.");
        }

        refreshTokens.Add(new RefreshToken(
            Guid.NewGuid(), user.Id, tokens.HashRefreshToken(refresh.Value), now, refresh.ExpiresAtUtc));

        return new AuthSessionResult(
            new AuthResponse(user.Id, access.Token, access.ExpiresAtUtc),
            refresh.Value,
            refresh.ExpiresAtUtc);
    }

    private static string NormalizeEmail(string email) => email.Trim().ToLowerInvariant();

    private static AppException EmailTaken() =>
        AppException.Conflict("auth.email_taken", "An account with this email already exists.");

    private static AppException InvalidCredentials() =>
        AppException.Unauthorized("auth.invalid_credentials", "Invalid email or password.");

    private static AppException InvalidSession() =>
        AppException.Unauthorized("auth.invalid_session", "The refresh session is invalid.");
}
