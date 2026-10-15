using CompanyHQ.ServiceCatalog.Contracts;
using CompanyHQ.Media.Contracts.Ports;

using CompanyHQ.Media.Contracts.Enums;
using CompanyHQ.Media.Contracts.Models;
using CompanyHQ.Persistence.Services;

namespace CompanyHQ.Media.Services;

/// <summary>Persists media lifecycle changes with the state predicate in the write itself.</summary>
public sealed class MediaObjectLifecycleStore(IServiceAvailability? availability,
    IRepository<MediaObject> mediaObjects,
    TimeProvider clock) : IMediaObjectLifecycleStore
{
    /// <summary>Retains the original strict constructor for existing consumers.</summary>
    public MediaObjectLifecycleStore(
    IRepository<MediaObject> mediaObjects,
    TimeProvider clock) : this(null, mediaObjects, clock) { }

    public async Task<bool> TryMarkReadyAsync(
        Guid mediaObjectId,
        MediaStatus expectedStatus,
        CancellationToken cancellationToken = default)
    {
        await (availability?.RequireAsync("media", true, cancellationToken) ?? Task.CompletedTask);
        var updated = await mediaObjects.Query()
            .Where(media => media.Id == mediaObjectId && media.Status == expectedStatus)
            .UpdateAsync(
                setters => setters
                    .Set(media => media.Status, MediaStatus.Ready)
                    .Set(media => media.UpdatedAt, clock.GetUtcNow()),
                cancellationToken);

        return updated == 1;
    }

    public async Task<bool> TryMarkDeletedAsync(
        Guid mediaObjectId,
        MediaStatus expectedStatus,
        CancellationToken cancellationToken = default)
    {
        await (availability?.RequireAsync("media", true, cancellationToken) ?? Task.CompletedTask);
        var updated = await mediaObjects.Query()
            .Where(media => media.Id == mediaObjectId && media.Status == expectedStatus)
            .UpdateAsync(
                setters => setters
                    .Set(media => media.Status, MediaStatus.Deleted)
                    .Set(media => media.UpdatedAt, clock.GetUtcNow()),
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
        await (availability?.RequireAsync("media", true, cancellationToken) ?? Task.CompletedTask);
        return await CompleteAcceptedCleanupFailureAsync(mediaObjectId, expectedFailureCount, nextFailureCount, nextStatus, cancellationToken);
    }

    // Internal completion records an already accepted provider result even if readiness changed.
    internal async Task<bool> CompleteAcceptedCleanupFailureAsync(Guid mediaObjectId, int expectedFailureCount,
        int nextFailureCount, MediaStatus nextStatus, CancellationToken cancellationToken)
    {
        var updated = await mediaObjects.Query()
            .Where(media => media.Id == mediaObjectId
                && media.Status == MediaStatus.Deleted
                && media.CleanupFailureCount == expectedFailureCount)
            .UpdateAsync(
                setters => setters
                    .Set(media => media.CleanupFailureCount, nextFailureCount)
                    .Set(media => media.Status, nextStatus)
                    .Set(media => media.UpdatedAt, clock.GetUtcNow()),
                cancellationToken);

        return updated == 1;
    }

    public async Task<bool> TryDeleteDeletedAsync(
        Guid mediaObjectId,
        CancellationToken cancellationToken = default)
    {
        await (availability?.RequireAsync("media", true, cancellationToken) ?? Task.CompletedTask);
        return await CompleteAcceptedDeleteAsync(mediaObjectId, cancellationToken);
    }

    internal async Task<bool> CompleteAcceptedDeleteAsync(Guid mediaObjectId, CancellationToken cancellationToken)
    {
        var deleted = await mediaObjects.Query()
            .Where(media => media.Id == mediaObjectId && media.Status == MediaStatus.Deleted)
            .DeleteAsync(cancellationToken);

        return deleted == 1;
    }
}
