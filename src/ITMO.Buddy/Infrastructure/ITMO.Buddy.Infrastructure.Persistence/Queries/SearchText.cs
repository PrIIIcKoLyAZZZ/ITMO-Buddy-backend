namespace ITMO.Buddy.Infrastructure.Persistence.Queries;

internal static class SearchText
{
    public const int MinLength = 2;
    public const int MaxLength = 200;

    public static string? Normalize(string? text)
    {
        var value = text?.Trim();

        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        if (value.Length < MinLength ||
            value.Length > MaxLength)
        {
            throw new ArgumentOutOfRangeException(
                nameof(text),
                $"Search text must contain between " +
                $"{MinLength} and {MaxLength} characters.");
        }

        return value;
    }

    public static string ToContainsPattern(string text)
    {
        ArgumentNullException.ThrowIfNull(text);

        var escaped = text
            .Replace("\\", "\\\\")
            .Replace("%", "\\%")
            .Replace("_", "\\_");

        return $"%{escaped}%";
    }
}