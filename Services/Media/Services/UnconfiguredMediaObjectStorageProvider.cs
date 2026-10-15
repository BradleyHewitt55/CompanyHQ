using CompanyHQ.Media.Contracts.Enums;
using CompanyHQ.Media.Contracts.Ports;
using CompanyHQ.ServiceCatalog.Contracts;

namespace CompanyHQ.Media.Services;

internal sealed class UnconfiguredMediaObjectStorageProvider : IMediaObjectStorageProvider
{
    private static ServiceUnavailableException Unavailable() => new("media", ServiceStatus.EnabledNotConfigured);
    public string CreateUploadUrl(string key, string contentType, DateTimeOffset expiresAt) => throw Unavailable();
    public string CreateDownloadUrl(string key, DateTimeOffset expiresAt) => throw Unavailable();
    public Task<bool> PutAsync(string key, string contentType, Stream content, long contentLength, CancellationToken cancellationToken = default) => throw Unavailable();
    public Task<StoredObjectMetadata?> GetMetadataAsync(string key, CancellationToken cancellationToken = default) => throw Unavailable();
    public Task<StorageDeleteOutcome> DeleteAsync(string key, CancellationToken cancellationToken = default) => throw Unavailable();
}
