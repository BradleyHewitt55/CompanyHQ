namespace CompanyHQ.Media.Contracts.Models;

/// <summary>Media storage, delivery, lifecycle, and validation settings.</summary>
public sealed class MediaOptions
{
    public const string SectionName = "Media";

    /// <summary>The bucket every object lives in, on the store the host's S3 client points at.</summary>
    public string Bucket { get; set; } = string.Empty;

    /// <summary>
    /// The HTTPS custom domain that exposes durable <c>user/</c> objects. Optional: when it is not set, public objects are addressed through this API's own
    /// <c>/api/media/{id}/content</c> route instead.
    /// </summary>
    public string PublicBaseUrl { get; set; } = string.Empty;

    /// <summary>
    /// Where this API is reachable from a phone, used to build <c>/api/media/{id}/content</c> URLs
    /// when no public storage domain is configured.
    /// </summary>
    public string ContentBaseUrl { get; set; } = string.Empty;

    public bool HasPublicDomain => !string.IsNullOrWhiteSpace(PublicBaseUrl);

    public long MaxUploadBytes { get; set; } = 10 * 1024 * 1024;

    public long MaxVideoUploadBytes { get; set; } = 100 * 1024 * 1024;

    public string[] AllowedContentTypes { get; set; } =
    [
        "image/jpeg", "image/png", "image/webp",
        "video/mp4", "video/webm", "video/quicktime", "video/x-m4v"
    ];

    public TimeSpan UploadUrlLifetime { get; set; } = TimeSpan.FromMinutes(15);

    public TimeSpan DownloadUrlLifetime { get; set; } = TimeSpan.FromHours(1);

    public TimeSpan PendingRetention { get; set; } = TimeSpan.FromHours(24);

    public TimeSpan CleanupInterval { get; set; } = TimeSpan.FromHours(1);

    public int CleanupBatchSize { get; set; } = 100;

    public int CleanupMaxAttempts { get; set; } = 5;

    public Uri GetPublicUrl(string key)
    {
        if (!key.StartsWith("user/", StringComparison.Ordinal))
        {
            throw new ArgumentException("Only user objects have durable public URLs.", nameof(key));
        }

        var escapedKey = string.Join('/', key.Split('/').Select(Uri.EscapeDataString));
        return new Uri($"{PublicBaseUrl.TrimEnd('/')}/{escapedKey}", UriKind.Absolute);
    }

    /// <summary>
    /// The durable route this API serves a public object from. It never expires, so it is safe to
    /// store on a profile the way a custom-domain URL would be.
    /// </summary>
    public Uri GetContentUrl(Guid mediaObjectId) =>
        new($"{ContentBaseUrl.TrimEnd('/')}/api/media/{mediaObjectId:D}/content", UriKind.Absolute);

    /// <summary>The address stored against a profile for a ready public object.</summary>
    public Uri GetDurableUrl(string key, Guid mediaObjectId) =>
        HasPublicDomain ? GetPublicUrl(key) : GetContentUrl(mediaObjectId);

    /// <summary>Throws during host registration so invalid media settings never reach a request.</summary>
    public void Validate() => Validate(false);

    public void Validate(bool allowUnconfigured)
    {
        if (!allowUnconfigured && string.IsNullOrWhiteSpace(Bucket))
        {
            throw new InvalidOperationException("Media:Bucket is required.");
        }

        if (HasPublicDomain)
        {
            if (!Uri.TryCreate(PublicBaseUrl, UriKind.Absolute, out var publicUri)
                || publicUri.Scheme != Uri.UriSchemeHttps
                || !string.IsNullOrEmpty(publicUri.Query)
                || !string.IsNullOrEmpty(publicUri.Fragment))
            {
                throw new InvalidOperationException("Media:PublicBaseUrl must be an absolute HTTPS URL without a query or fragment.");
            }

        }
        else if ((!allowUnconfigured || !string.IsNullOrWhiteSpace(ContentBaseUrl))
            && (!Uri.TryCreate(ContentBaseUrl, UriKind.Absolute, out var contentUri)
            || (contentUri.Scheme != Uri.UriSchemeHttps && contentUri.Scheme != Uri.UriSchemeHttp)
            || !string.IsNullOrEmpty(contentUri.Query)
            || !string.IsNullOrEmpty(contentUri.Fragment)))
        {
            throw new InvalidOperationException(
                "Media needs somewhere to serve public media from: set Media:PublicBaseUrl to a public storage domain, "
                + "or Media:ContentBaseUrl to where this API is reachable.");
        }

        if (MaxUploadBytes <= 0 || MaxVideoUploadBytes <= 0)
        {
            throw new InvalidOperationException("Media upload limits must be greater than zero.");
        }
        if (AllowedContentTypes is null || AllowedContentTypes.Length == 0 || AllowedContentTypes.Any(string.IsNullOrWhiteSpace))
        {
            throw new InvalidOperationException("Media:AllowedContentTypes must contain at least one non-empty content type.");
        }

        if (UploadUrlLifetime <= TimeSpan.Zero || UploadUrlLifetime > TimeSpan.FromDays(7)
            || DownloadUrlLifetime <= TimeSpan.Zero || DownloadUrlLifetime > TimeSpan.FromDays(7)
            || PendingRetention <= TimeSpan.Zero || CleanupInterval <= TimeSpan.Zero
            || CleanupBatchSize <= 0 || CleanupMaxAttempts <= 0)
        {
            throw new InvalidOperationException("Media upload, download, and cleanup settings must be greater than zero; URL lifetimes cannot exceed seven days.");
        }
    }
}
