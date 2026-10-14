using Microsoft.Extensions.Logging;

using CompanyHQ.Media.Contracts.Enums;
using CompanyHQ.Media.Contracts.Models;
using CompanyHQ.Media.Contracts.Ports;
using CompanyHQ.Persistence.Services;

namespace CompanyHQ.Media.Services;

/// <summary>
/// Removes one batch of abandoned media objects after their lifecycle rows are marked deleted.
/// Scoped and resolved per pass by <see cref="MediaCleanupWorker"/>; taking the storage adapter in
/// the constructor is what lets the host's build-time validation refuse to start without one.
/// </summary>
internal sealed class MediaCleanupProcessor(
    IRepository<MediaObject> mediaObjects,
    IMediaObjectLifecycleStore lifecycle,
    IMediaObjectStorageProvider storage,
    MediaOptions options,
    TimeProvider clock,
    ILogger<MediaCleanupProcessor> logger)
{
    public async Task<int> CleanupBatchAsync(CancellationToken cancellationToken)
    {
        var cutoff = clock.GetUtcNow().Subtract(options.PendingRetention);
        var candidates = await mediaObjects.Query()
            .Where(media => media.Status == MediaStatus.Deleted
                || (media.Status == MediaStatus.Pending && media.CreatedAt <= cutoff))
            .OrderBy(media => media.CreatedAt)
            .Take(options.CleanupBatchSize)
            .ToListAsync(cancellationToken);

        var removed = 0;
        foreach (var media in candidates)
        {
            if (!await lifecycle.TryMarkDeletedAsync(media.Id, media.Status, cancellationToken)) continue;

            var outcome = await storage.DeleteAsync(media.Key, cancellationToken);
            if (outcome is StorageDeleteOutcome.Transient)
            {
                logger.LogWarning("Media cleanup hit a transient storage delete failure for {MediaObjectId}; retrying later.", media.Id);
                continue;
            }

            if (outcome is StorageDeleteOutcome.Permanent)
            {
                var nextFailureCount = media.CleanupFailureCount + 1;
                var nextStatus = nextFailureCount >= options.CleanupMaxAttempts ? MediaStatus.Failed : MediaStatus.Deleted;
                if (await lifecycle.TryRecordCleanupFailureAsync(
                    media.Id, media.CleanupFailureCount, nextFailureCount, nextStatus, cancellationToken))
                {
                    logger.LogError("Media cleanup retained {MediaObjectId} after permanent storage failure ({Attempt}/{Maximum}).", media.Id, nextFailureCount, options.CleanupMaxAttempts);
                }

                continue;
            }

            if (await lifecycle.TryDeleteDeletedAsync(media.Id, cancellationToken)) removed++;
        }

        return removed;
    }
}
