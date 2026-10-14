using CompanyHQ.Media.Contracts.Enums;

namespace CompanyHQ.Media.Contracts.Ports;

/// <summary>
/// Storage operations for image and video objects. Media fills it itself with the host's S3 client;
/// it stays a port so the lifecycle can be exercised without an object store.
/// </summary>
public interface IMediaObjectStorageProvider
{
    string CreateUploadUrl(string key, string contentType, DateTimeOffset expiresAt);

    string CreateDownloadUrl(string key, DateTimeOffset expiresAt);

    Task<bool> PutAsync(
        string key,
        string contentType,
        Stream content,
        long contentLength,
        CancellationToken cancellationToken = default);

    Task<StoredObjectMetadata?> GetMetadataAsync(
        string key,
        CancellationToken cancellationToken = default);

    Task<StorageDeleteOutcome> DeleteAsync(
        string key,
        CancellationToken cancellationToken = default);
}
