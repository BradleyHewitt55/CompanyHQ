using System.Net;
using System.Net.Http.Headers;

using Amazon.Runtime;
using Amazon.S3;
using Amazon.S3.Model;

using CompanyHQ.Media.Contracts.Enums;
using CompanyHQ.Media.Contracts.Models;
using CompanyHQ.Media.Contracts.Ports;

namespace CompanyHQ.Media.Services;

/// <summary>
/// Media's object-storage port over the host's <see cref="IAmazonS3"/> client. The host points that
/// client at its S3-compatible store (Cloudflare R2); this class only names the bucket and the object.
/// </summary>
internal sealed class S3MediaObjectStorageProvider(IAmazonS3 s3, MediaOptions options)
    : IMediaObjectStorageProvider
{
    // The content type is signed, so the uploader must send exactly the one the ticket declared.
    public string CreateUploadUrl(string key, string contentType, DateTimeOffset expiresAt) =>
        s3.GetPreSignedURL(new GetPreSignedUrlRequest
        {
            BucketName = options.Bucket,
            Key = key,
            Verb = HttpVerb.PUT,
            ContentType = contentType,
            Expires = expiresAt.UtcDateTime
        });

    public string CreateDownloadUrl(string key, DateTimeOffset expiresAt) =>
        s3.GetPreSignedURL(new GetPreSignedUrlRequest
        {
            BucketName = options.Bucket,
            Key = key,
            Verb = HttpVerb.GET,
            Expires = expiresAt.UtcDateTime
        });

    public async Task<bool> PutAsync(
        string key,
        string contentType,
        Stream content,
        long contentLength,
        CancellationToken cancellationToken = default)
    {
        var request = new PutObjectRequest
        {
            BucketName = options.Bucket,
            Key = key,
            InputStream = content,
            ContentType = contentType,
            AutoCloseStream = false,
            // The request body is forwarded as it arrives and cannot be rewound to hash it, and R2
            // does not accept the chunk-signed alternative.
            DisablePayloadSigning = true
        };
        request.Headers.ContentLength = contentLength;

        try
        {
            await s3.PutObjectAsync(request, cancellationToken);
            return true;
        }
        catch (AmazonS3Exception)
        {
            return false;
        }
    }

    public async Task<StoredObjectMetadata?> GetMetadataAsync(
        string key,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await s3.GetObjectMetadataAsync(options.Bucket, key, cancellationToken);

            return new StoredObjectMetadata(
                MediaTypeHeaderValue.TryParse(response.Headers.ContentType, out var contentType)
                    ? contentType.MediaType ?? string.Empty
                    : string.Empty,
                response.ContentLength);
        }
        catch (AmazonS3Exception exception) when (exception.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }
    }

    public async Task<StorageDeleteOutcome> DeleteAsync(
        string key,
        CancellationToken cancellationToken = default)
    {
        try
        {
            await s3.DeleteObjectAsync(options.Bucket, key, cancellationToken);
            return StorageDeleteOutcome.AlreadyGone;
        }
        catch (AmazonServiceException exception) when (exception.StatusCode == HttpStatusCode.NotFound)
        {
            return StorageDeleteOutcome.AlreadyGone;
        }
        catch (AmazonServiceException exception)
        {
            // No status at all means the store was never reached.
            return (int)exception.StatusCode is >= 400 and < 500
                ? StorageDeleteOutcome.Permanent
                : StorageDeleteOutcome.Transient;
        }
        catch (HttpRequestException)
        {
            return StorageDeleteOutcome.Transient;
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return StorageDeleteOutcome.Transient;
        }
    }
}
