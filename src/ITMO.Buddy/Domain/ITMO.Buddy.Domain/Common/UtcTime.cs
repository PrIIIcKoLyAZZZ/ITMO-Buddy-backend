namespace ITMO.Buddy.Domain.Common;

internal static class UtcTime
{
    public static DateTimeOffset Require(
        DateTimeOffset value,
        string parameterName)
    {
        if (value == default || value.Offset != TimeSpan.Zero)
        {
            throw new DomainException(
                "common.utc.required",
                $"{parameterName} must be a valid UTC timestamp.");
        }

        return value;
    }
}