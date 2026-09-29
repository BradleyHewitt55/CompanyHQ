using CompanyHQ.Services.Media.Contracts.Enums;

namespace CompanyHQ.Services.Media.Services;

/// <summary>Applies media lifecycle transitions only while the expected state still holds.</summary>
public interface IMediaObjectLifecycleStore
{
    Task<bool> TryMarkReadyAsync(
        Guid mediaObjectId,
        MediaStatus expectedStatus,
        CancellationToken cancellationToken = default);

    Task<bool> TryMarkDeletedAsync(
        Guid mediaObjectId,
        MediaStatus expectedStatus,
        CancellationToken cancellationToken = default);

    Task<bool> TryRecordCleanupFailureAsync(
        Guid mediaObjectId,
        int expectedFailureCount,
        int nextFailureCount,
        MediaStatus nextStatus,
        CancellationToken cancellationToken = default);

    Task<bool> TryDeleteDeletedAsync(
        Guid mediaObjectId,
        CancellationToken cancellationToken = default);
}
