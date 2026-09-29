using CompanyHQ.Services.Media.Contracts.Enums;

namespace CompanyHQ.Services.Media.Contracts.Ports;

/// <summary>
/// Provider-neutral storage operations for image and video objects. An adapter service (Cloudflare)
/// implements it by referencing this Contracts assembly only. Required: without an adapter the host
/// fails its build-time validation.
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
