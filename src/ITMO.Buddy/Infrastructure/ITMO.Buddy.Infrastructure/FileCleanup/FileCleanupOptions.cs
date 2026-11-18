namespace ITMO.Buddy.Infrastructure.FileCleanup;

public sealed class FileCleanupOptions
{
    public bool Enabled { get; set; }
    public TimeSpan PollInterval { get; set; } = TimeSpan.FromSeconds(30);
    public int BatchSize { get; set; } = 20;
    public TimeSpan MinimumEntryAge { get; set; } = TimeSpan.FromMinutes(2);
    public TimeSpan OperationTimeout { get; set; } = TimeSpan.FromSeconds(45);
    public TimeSpan ReconciliationInterval { get; set; } = TimeSpan.FromHours(1);
    public TimeSpan ReconciliationMinimumObjectAge { get; set; } = TimeSpan.FromMinutes(10);

    internal void Validate()
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(BatchSize, 1);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(BatchSize, 100);
        ValidateDuration(PollInterval, nameof(PollInterval));
        ValidateDuration(MinimumEntryAge, nameof(MinimumEntryAge));
        ValidateDuration(OperationTimeout, nameof(OperationTimeout));
        ValidateDuration(ReconciliationInterval, nameof(ReconciliationInterval));
        ValidateDuration(ReconciliationMinimumObjectAge, nameof(ReconciliationMinimumObjectAge));
    }

    private static void ValidateDuration(TimeSpan duration, string name)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(duration, TimeSpan.FromSeconds(1), name);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(duration, TimeSpan.FromDays(30), name);
    }
}
