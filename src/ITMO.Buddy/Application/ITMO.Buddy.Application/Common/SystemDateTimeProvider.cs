using ITMO.Buddy.Application.Abstractions.Time;

namespace ITMO.Buddy.Application.Common;

internal sealed class SystemDateTimeProvider : IDateTimeProvider
{
    public DateTimeOffset UtcNow => TimeProvider.System.GetUtcNow();
}
