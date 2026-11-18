using ITMO.Buddy.Application.Errors;

namespace ITMO.Buddy.Application.Common;

internal static class ContractEnum<TEnum> where TEnum : struct, Enum
{
    private static readonly Dictionary<string, TEnum> Values =
        Enum.GetValues<TEnum>().ToDictionary(
            value => value.ToString(),
            StringComparer.OrdinalIgnoreCase);

    public static bool IsValid(string? value) => TryParse(value, out _);

    public static TEnum Parse(string? value) =>
        TryParse(value, out var parsed)
            ? parsed
            : throw AppException.Validation("request.enum.invalid", "An enum value is invalid.");

    private static bool TryParse(string? value, out TEnum parsed) =>
        Values.TryGetValue(value?.Trim() ?? string.Empty, out parsed);
}
