using CompanyHQ.Services.Media.Contracts.Ports;
using Microsoft.EntityFrameworkCore;

using CompanyHQ.Services.Media.Contracts.Enums;
using CompanyHQ.Services.Media.Contracts.Models;
using CompanyHQ.Services.Postgres.Services;

namespace CompanyHQ.Services.Media.Services;

/// <summary>Persists media lifecycle changes with the state predicate in the write itself.</summary>
public sealed class MediaObjectLifecycleStore(
    IPostgresService<MediaObject> mediaObjects,
    TimeProvider clock) : IMediaObjectLifecycleStore
{
    public async Task<bool> TryMarkReadyAsync(
        Guid mediaObjectId,
        MediaStatus expectedStatus,
        CancellationToken cancellationToken = default)
    {
        var updated = await mediaObjects.Query()
            .Where(media => media.Id == mediaObjectId && media.Status == expectedStatus)
            .ExecuteUpdateAsync(
                setters => setters
                    .SetProperty(media => media.Status, MediaStatus.Ready)
                    .SetProperty(media => media.UpdatedAt, clock.GetUtcNow()),
                cancellationToken);

        return updated == 1;
    }

    public async Task<bool> TryMarkDeletedAsync(
        Guid mediaObjectId,
        MediaStatus expectedStatus,
        CancellationToken cancellationToken = default)
    {
        var updated = await mediaObjects.Query()
            .Where(media => media.Id == mediaObjectId && media.Status == expectedStatus)
            .ExecuteUpdateAsync(
                setters => setters
                    .SetProperty(media => media.Status, MediaStatus.Deleted)
                    .SetProperty(media => media.UpdatedAt, clock.GetUtcNow()),
                cancellationToken);

        return updated == 1;
    }

    public async Task<bool> TryRecordCleanupFailureAsync(
        Guid mediaObjectId,
        int expectedFailureCount,
        int nextFailureCount,
        MediaStatus nextStatus,
        CancellationToken cancellationToken = default)
    {
        var updated = await mediaObjects.Query()
            .Where(media => media.Id == mediaObjectId
                && media.Status == MediaStatus.Deleted
                && media.CleanupFailureCount == expectedFailureCount)
            .ExecuteUpdateAsync(
                setters => setters
                    .SetProperty(media => media.CleanupFailureCount, nextFailureCount)
                    .SetProperty(media => media.Status, nextStatus)
                    .SetProperty(media => media.UpdatedAt, clock.GetUtcNow()),
                cancellationToken);

        return updated == 1;
    }

    public async Task<bool> TryDeleteDeletedAsync(
        Guid mediaObjectId,
        CancellationToken cancellationToken = default)
    {
        var deleted = await mediaObjects.Query()
            .Where(media => media.Id == mediaObjectId && media.Status == MediaStatus.Deleted)
            .ExecuteDeleteAsync(cancellationToken);

        return deleted == 1;
    }
}
