using System.Globalization;
using System.Text.RegularExpressions;
using ITMO.Buddy.Application.Abstractions.Persistence;
using ITMO.Buddy.Application.Abstractions.Storage;
using ITMO.Buddy.Application.Abstractions.Time;
using ITMO.Buddy.Infrastructure.Storage;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ITMO.Buddy.Infrastructure.FileCleanup;

internal sealed partial class FileCleanupBackgroundService(
    IServiceScopeFactory scopes,
    IDateTimeProvider clock,
    IOptions<FileCleanupOptions> options,
    ILogger<FileCleanupBackgroundService> logger) : BackgroundService
{
    private static readonly TimeSpan ErrorLogInterval = TimeSpan.FromMinutes(5);
    private readonly FileCleanupOptions _settings = options.Value;
    private readonly Dictionary<(string Operation, string Reason), long> _lastFailureLogs = new();

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _settings.Validate();
        if (!_settings.Enabled)
        {
            logger.LogInformation("File cleanup worker is disabled.");
            return;
        }

        logger.LogInformation("File cleanup worker started.");
        try
        {
            await Task.WhenAll(ProcessQueueAsync(stoppingToken), ReconcileAsync(stoppingToken));
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // The host requested shutdown.
        }
        finally
        {
            logger.LogInformation("File cleanup worker stopped.");
        }
    }

    private async Task ProcessQueueAsync(CancellationToken stoppingToken)
    {
        FileCleanupCandidate? cursor = null;
        var cutoffUtc = clock.UtcNow - _settings.MinimumEntryAge;
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var page = await ReadPageAsync(cutoffUtc, cursor, stoppingToken);
                var attempted = 0;
                var committed = 0;
                foreach (var candidate in page)
                {
                    stoppingToken.ThrowIfCancellationRequested();
                    cursor = candidate;
                    attempted++;

                    try
                    {
                        if (await ProcessCandidateAsync(candidate, stoppingToken))
                        {
                            committed++;
                            logger.LogDebug("File cleanup deletion committed.");
                        }
                    }
                    catch (Exception error) when (!stoppingToken.IsCancellationRequested)
                    {
                        LogFailure("queue item", error);
                        if (error is PersistenceException { Code: PersistenceErrorCode.ConnectionFailure })
                        {
                            break;
                        }
                    }
                }

                if (attempted == page.Count && page.Count < _settings.BatchSize)
                {
                    cursor = null;
                    cutoffUtc = clock.UtcNow - _settings.MinimumEntryAge;
                }

                if (attempted > 0)
                {
                    logger.LogDebug("File cleanup attempted {Attempted} jobs and committed {Committed} deletions.",
                        attempted, committed);
                }
            }
            catch (Exception error) when (!stoppingToken.IsCancellationRequested)
            {
                LogFailure("PostgreSQL queue read", error);
            }

            await Task.Delay(_settings.PollInterval, stoppingToken);
        }
    }

    private async Task<IReadOnlyList<FileCleanupCandidate>> ReadPageAsync(
        DateTimeOffset cutoffUtc, FileCleanupCandidate? cursor, CancellationToken stoppingToken)
    {
        using var timeout = StartOperation(stoppingToken);
        await using var scope = scopes.CreateAsyncScope();
        var queue = scope.ServiceProvider.GetRequiredService<IFileCleanupQueue>();
        return await queue.GetCandidatesAsync(cutoffUtc, cursor, _settings.BatchSize, timeout.Token);
    }

    private async Task<bool> ProcessCandidateAsync(FileCleanupCandidate candidate, CancellationToken stoppingToken)
    {
        using var timeout = StartOperation(stoppingToken);
        await using var scope = scopes.CreateAsyncScope();
        var queue = scope.ServiceProvider.GetRequiredService<IFileCleanupQueue>();
        var transactions = scope.ServiceProvider.GetRequiredService<ITransactionRunner>();
        var files = scope.ServiceProvider.GetRequiredService<IFileStorage>();
        try
        {
            return await transactions.ExecuteAsync(async token =>
            {
                if (!await queue.TryTakeAsync(candidate, token))
                {
                    return false;
                }

                if (await queue.HasProfileReferenceAsync(candidate.ObjectKey, token))
                {
                    // The runner only returns this exception after a successful rollback.
                    throw new PhotoStillReferencedException();
                }

                await files.DeleteAsync(candidate.ObjectKey, token);
                return true;
            }, timeout.Token);
        }
        catch (PhotoStillReferencedException)
        {
            return false;
        }
    }

    private async Task ReconcileAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var enqueued = await ReconcileBucketAsync(stoppingToken);
                logger.LogInformation("File cleanup reconciliation completed and enqueued {OrphanCount} orphan candidates.", enqueued);
            }
            catch (Exception error) when (!stoppingToken.IsCancellationRequested)
            {
                LogFailure("reconciliation incomplete", error);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                logger.LogInformation("File cleanup reconciliation stopped before completing.");
                throw;
            }

            await Task.Delay(_settings.ReconciliationInterval, stoppingToken);
        }
    }

    private async Task<long> ReconcileBucketAsync(CancellationToken stoppingToken)
    {
        var cutoffUtc = clock.UtcNow - _settings.ReconciliationMinimumObjectAge;
        var keys = new List<string>(_settings.BatchSize);
        long enqueued = 0;
        using var listingCancellation = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);
        await using var storageScope = scopes.CreateAsyncScope();
        var storage = storageScope.ServiceProvider.GetRequiredService<MinioFileStorage>();
        await using var listing = storage.ListProfileObjectsAsync(listingCancellation.Token).GetAsyncEnumerator();
        while (true)
        {
            listingCancellation.CancelAfter(_settings.OperationTimeout);
            var hasItem = await listing.MoveNextAsync();
            listingCancellation.CancelAfter(Timeout.InfiniteTimeSpan);
            listingCancellation.Token.ThrowIfCancellationRequested();
            if (!hasItem)
            {
                break;
            }

            var item = listing.Current;
            if (item.IsDir || item.Key is not { } key || !ProfilePhotoKey().IsMatch(key) ||
                !DateTimeOffset.TryParseExact(item.LastModified, "yyyy-MM-dd'T'HH:mm:ss.FFFFFFF'Z'",
                    CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
                    out var modifiedUtc) ||
                modifiedUtc > cutoffUtc)
            {
                continue;
            }

            keys.Add(key);
            if (keys.Count == _settings.BatchSize)
            {
                enqueued += await EnqueueOrphansAsync(keys, stoppingToken);
                keys.Clear();
            }
        }

        if (keys.Count > 0)
        {
            enqueued += await EnqueueOrphansAsync(keys, stoppingToken);
        }

        return enqueued;
    }

    private async Task<int> EnqueueOrphansAsync(IReadOnlyCollection<string> keys, CancellationToken stoppingToken)
    {
        using var timeout = StartOperation(stoppingToken);
        await using var scope = scopes.CreateAsyncScope();
        var queue = scope.ServiceProvider.GetRequiredService<IFileCleanupQueue>();
        try
        {
            return await queue.EnqueueUnreferencedAsync(keys, timeout.Token);
        }
        catch (Exception error) when (!stoppingToken.IsCancellationRequested)
        {
            LogFailure("PostgreSQL reconciliation enqueue", error);
            throw;
        }
    }

    private CancellationTokenSource StartOperation(CancellationToken stoppingToken)
    {
        var timeout = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);
        timeout.CancelAfter(_settings.OperationTimeout);
        return timeout;
    }

    private void LogFailure(string operation, Exception error)
    {
        var reason = error switch
        {
            PersistenceException persistence => persistence.Code.ToString(),
            MinioStorageException storage => storage.Failure.ToString(),
            OperationCanceledException => "OperationTimeout",
            _ => error.GetType().Name
        };

        var key = (operation, reason);
        var time = TimeProvider.System.GetTimestamp();
        lock (_lastFailureLogs)
        {
            if (_lastFailureLogs.TryGetValue(key, out var previous) &&
                TimeProvider.System.GetElapsedTime(previous, time) < ErrorLogInterval)
            {
                return;
            }

            _lastFailureLogs[key] = time;
        }

        logger.LogWarning("File cleanup {Operation} failed ({Reason}).", operation, reason);
    }

    [GeneratedRegex(@"\Aprofiles/[0-9a-f]{32}\.(jpg|png)\z")]
    private static partial Regex ProfilePhotoKey();

    private sealed class PhotoStillReferencedException : Exception;
}
