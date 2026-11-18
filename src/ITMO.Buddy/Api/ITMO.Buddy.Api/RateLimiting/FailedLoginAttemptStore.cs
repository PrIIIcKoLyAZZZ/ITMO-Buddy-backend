namespace ITMO.Buddy.Api.RateLimiting;

internal sealed class FailedLoginAttemptStore(RateLimitingOptions options) : BackgroundService
{
    private readonly Dictionary<string, Entry> _entries = new(StringComparer.Ordinal);
    private readonly Lock _entriesLock = new();
    private readonly TimeProvider _clock = TimeProvider.System;

    public async ValueTask<Lease?> AcquireAsync(string key, CancellationToken cancellationToken)
    {
        Entry entry;
        lock (_entriesLock)
        {
            if (!_entries.TryGetValue(key, out var existing))
            {
                if (_entries.Count >= options.MaxTrackedLoginIps)
                {
                    return null;
                }

                entry = new Entry();
                _entries.Add(key, entry);
            }
            else
            {
                entry = existing;
            }

            // Keep the entry alive while the caller owns or is waiting for its semaphore.
            entry.References++;
        }

        try
        {
            await entry.Gate.WaitAsync(cancellationToken);
            return new Lease(this, key, entry);
        }
        catch
        {
            ReleaseReference(key, entry);
            throw;
        }
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(30));
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            lock (_entriesLock)
            {
                foreach (var (key, entry) in _entries)
                {
                    if (entry.References == 0)
                    {
                        RemoveExpired(entry, _clock.GetTimestamp());
                        RemoveIfIdle(key, entry);
                    }
                }
            }
        }
    }

    private void ReleaseReference(string key, Entry entry)
    {
        lock (_entriesLock)
        {
            entry.References--;
            RemoveIfIdle(key, entry);
        }
    }

    private void RemoveIfIdle(string key, Entry entry)
    {
        if (entry.References == 0 && entry.Failures.Count == 0)
        {
            _entries.Remove(key);
            entry.Gate.Dispose();
        }
    }

    private void RemoveExpired(Entry entry, long now)
    {
        while (entry.Failures.TryPeek(out var first) &&
               _clock.GetElapsedTime(first, now) >= options.LoginFailureWindow)
        {
            entry.Failures.Dequeue();
        }
    }

    internal sealed class Entry
    {
        public SemaphoreSlim Gate { get; } = new(1, 1);
        public Queue<long> Failures { get; } = new();
        public int References { get; set; }
    }

    internal sealed class Lease(FailedLoginAttemptStore store, string key, Entry entry) : IDisposable
    {
        public TimeSpan? GetRetryAfter()
        {
            var now = store._clock.GetTimestamp();
            store.RemoveExpired(entry, now);
            return entry.Failures.Count >= store.FailureLimit
                ? store.FailureWindow - store._clock.GetElapsedTime(entry.Failures.Peek(), now)
                : null;
        }

        public void RecordFailure() => entry.Failures.Enqueue(store._clock.GetTimestamp());

        public void Dispose()
        {
            entry.Gate.Release();
            store.ReleaseReference(key, entry);
        }
    }

    private int FailureLimit => options.LoginFailureCount;
    private TimeSpan FailureWindow => options.LoginFailureWindow;
}
