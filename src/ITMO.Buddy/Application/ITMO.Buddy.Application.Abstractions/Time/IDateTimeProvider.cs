namespace ITMO.Buddy.Application.Abstractions.Time;

/// <summary>
/// Абстракция получения текущего времени UTC.
/// </summary>
public interface IDateTimeProvider
{
    /// <summary>
    /// Текущее время в UTC.
    /// Значение должно иметь нулевое смещение.
    /// </summary>
    DateTimeOffset UtcNow { get; }
}