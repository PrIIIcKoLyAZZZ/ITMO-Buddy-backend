using System.Buffers.Text;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Cryptography;
using System.Text;
using ITMO.Buddy.Application.Abstractions.Security;
using ITMO.Buddy.Domain.Enums;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace ITMO.Buddy.Infrastructure.Security;

internal sealed class JwtTokenService : ITokenService
{
    private const int MinimumSigningKeyBytes = 32;
    private const int RefreshTokenBytes = 32;
    private static readonly TimeSpan RefreshLifetime = TimeSpan.FromDays(14);
    private readonly string _issuer;
    private readonly string _audience;
    private readonly TimeSpan _accessLifetime;
    private readonly SigningCredentials _signingCredentials;

    public JwtTokenService(IOptions<JwtOptions> options)
    {
        ArgumentNullException.ThrowIfNull(options);
        var settings = options.Value;
        ArgumentException.ThrowIfNullOrWhiteSpace(settings.Issuer, nameof(JwtOptions.Issuer));
        ArgumentException.ThrowIfNullOrWhiteSpace(settings.Audience, nameof(JwtOptions.Audience));
        if (settings.AccessTokenLifetime < TimeSpan.FromSeconds(1) ||
            settings.AccessTokenLifetime > TimeSpan.FromMinutes(30))
        {
            throw new ArgumentException("JWT access token lifetime must be between one second and 30 minutes.",
                nameof(JwtOptions.AccessTokenLifetime));
        }

        _issuer = settings.Issuer;
        _audience = settings.Audience;
        _accessLifetime = settings.AccessTokenLifetime;
        _signingCredentials = new SigningCredentials(ReadSigningKey(settings.SigningKey), SecurityAlgorithms.HmacSha256);
    }

    public AccessTokenIssue CreateAccessToken(Guid userId, UserRole role, DateTimeOffset issuedAtUtc)
    {
        var issuedAt = issuedAtUtc.ToUnixTimeSeconds();
        var expiresAt = issuedAtUtc.Add(_accessLifetime).ToUnixTimeSeconds();
        var payload = new JwtPayload
        {
            [JwtRegisteredClaimNames.Sub] = userId.ToString("D"),
            ["role"] = role.ToString(),
            [JwtRegisteredClaimNames.Iss] = _issuer,
            [JwtRegisteredClaimNames.Aud] = _audience,
            [JwtRegisteredClaimNames.Iat] = issuedAt,
            [JwtRegisteredClaimNames.Nbf] = issuedAt,
            [JwtRegisteredClaimNames.Exp] = expiresAt
        };
        var jwt = new JwtSecurityToken(new JwtHeader(_signingCredentials), payload);
        var token = new JwtSecurityTokenHandler().WriteToken(jwt);
        return new AccessTokenIssue(token, DateTimeOffset.FromUnixTimeSeconds(expiresAt));
    }

    public RefreshTokenIssue CreateRefreshToken(DateTimeOffset issuedAtUtc)
    {
        Span<byte> bytes = stackalloc byte[RefreshTokenBytes];
        RandomNumberGenerator.Fill(bytes);
        return new RefreshTokenIssue(Base64Url.EncodeToString(bytes), issuedAtUtc.ToUniversalTime().Add(RefreshLifetime));
    }

    public string HashRefreshToken(string refreshToken)
    {
        ArgumentNullException.ThrowIfNull(refreshToken);
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(refreshToken)));
    }

    private static SymmetricSecurityKey ReadSigningKey(string signingKey)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(signingKey, nameof(JwtOptions.SigningKey));
        byte[] keyBytes;
        try
        {
            keyBytes = Convert.FromBase64String(signingKey);
        }
        catch (FormatException)
        {
            throw new ArgumentException("JWT signing key must be valid Base64.", nameof(JwtOptions.SigningKey));
        }

        if (keyBytes.Length < MinimumSigningKeyBytes)
        {
            throw new ArgumentException("JWT signing key must decode to at least 32 bytes.", nameof(JwtOptions.SigningKey));
        }

        return new SymmetricSecurityKey(keyBytes);
    }
}
