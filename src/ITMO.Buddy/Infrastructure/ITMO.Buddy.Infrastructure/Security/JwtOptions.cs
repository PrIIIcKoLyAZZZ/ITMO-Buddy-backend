using System.Security.Cryptography;

namespace ITMO.Buddy.Infrastructure.Security;

public sealed class JwtOptions
{
    public string Issuer { get; set; } = string.Empty;
    public string Audience { get; set; } = string.Empty;
    public string SigningKey { get; set; } = string.Empty;
    public TimeSpan AccessTokenLifetime { get; set; } = TimeSpan.FromMinutes(30);

    internal byte[] ValidateAndReadSigningKey()
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(Issuer, nameof(Issuer));
        ArgumentException.ThrowIfNullOrWhiteSpace(Audience, nameof(Audience));
        if (AccessTokenLifetime < TimeSpan.FromSeconds(1) || AccessTokenLifetime > TimeSpan.FromMinutes(30))
        {
            throw new ArgumentException("JWT access token lifetime must be between one second and 30 minutes.",
                nameof(AccessTokenLifetime));
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(SigningKey, nameof(SigningKey));
        byte[] keyBytes;
        try
        {
            keyBytes = Convert.FromBase64String(SigningKey);
        }
        catch (FormatException)
        {
            throw new ArgumentException("JWT signing key must be valid Base64.", nameof(SigningKey));
        }

        if (keyBytes.Length < 32)
        {
            CryptographicOperations.ZeroMemory(keyBytes);
            throw new ArgumentException("JWT signing key must decode to at least 32 bytes.", nameof(SigningKey));
        }

        return keyBytes;
    }
}
