using System.Text;
using System.Text.RegularExpressions;
using BCrypt.Net;
using ITMO.Buddy.Application.Abstractions.Security;

namespace ITMO.Buddy.Infrastructure.Security;

internal sealed partial class BCryptPasswordHasher : IPasswordHasher
{
    private const int WorkFactor = 12;
    private const int MaxPasswordBytes = 72;
    private static readonly Encoding Utf8 = new UTF8Encoding(false, throwOnInvalidBytes: true);

    public string Hash(string password)
    {
        ArgumentNullException.ThrowIfNull(password);
        int byteCount;
        try
        {
            byteCount = Utf8.GetByteCount(password);
        }
        catch (EncoderFallbackException)
        {
            throw new ArgumentException("Password must be valid Unicode.", nameof(password));
        }

        if (byteCount > MaxPasswordBytes)
        {
            throw new ArgumentException("Password must be at most 72 bytes in UTF-8.", nameof(password));
        }

        return BCrypt.Net.BCrypt.HashPassword(password, WorkFactor);
    }

    public bool Verify(string password, string passwordHash)
    {
        if (password is null || passwordHash is not { Length: 60 } || !HashFormat().IsMatch(passwordHash))
        {
            return false;
        }

        try
        {
            return BCrypt.Net.BCrypt.Verify(password, passwordHash);
        }
        catch (Exception error) when (error is SaltParseException or ArgumentException)
        {
            return false;
        }
    }

    // Bound verification cost even when a stored hash has been corrupted.
    [GeneratedRegex(@"\A\$2[abxy]\$(0[4-9]|1[0-6])\$[./A-Za-z0-9]{53}\z")]
    private static partial Regex HashFormat();
}
