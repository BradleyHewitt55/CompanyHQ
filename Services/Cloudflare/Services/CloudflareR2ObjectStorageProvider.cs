using System.Globalization;
using System.Security.Cryptography;
using System.Text;

using CompanyHQ.Services.Cloudflare.Contracts.Models;
using CompanyHQ.Services.Media.Contracts.Enums;
using CompanyHQ.Services.Media.Contracts.Ports;

namespace CompanyHQ.Services.Cloudflare.Services;

/// <summary>Cloudflare R2 implementation of Media's provider-neutral object-storage port.</summary>
public sealed class CloudflareR2ObjectStorageProvider(IHttpClientFactory clients, R2Options options)
    : IMediaObjectStorageProvider
{
    private const string Algorithm = "AWS4-HMAC-SHA256";
    private const string Region = "auto";
    private const string Service = "s3";

    public string CreateUploadUrl(string key, string contentType, DateTimeOffset expiresAt) =>
        CreatePresignedUrl(key, HttpMethod.Put, expiresAt, contentType);

    public string CreateDownloadUrl(string key, DateTimeOffset expiresAt) =>
        CreatePresignedUrl(key, HttpMethod.Get, expiresAt);

    public async Task<bool> PutAsync(
        string key,
        string contentType,
        Stream content,
        long contentLength,
        CancellationToken cancellationToken = default)
    {
        using var request = new HttpRequestMessage(
            HttpMethod.Put,
            CreateUploadUrl(key, contentType, DateTimeOffset.UtcNow.AddMinutes(5)))
        {
            Content = new StreamContent(content)
        };
        request.Content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue(contentType);
        request.Content.Headers.ContentLength = contentLength;

        using var response = await clients.CreateClient().SendAsync(
            request,
            HttpCompletionOption.ResponseHeadersRead,
            cancellationToken);
        return response.IsSuccessStatusCode;
    }

    public async Task<StoredObjectMetadata?> GetMetadataAsync(
        string key,
        CancellationToken cancellationToken = default)
    {
        using var request = new HttpRequestMessage(
            HttpMethod.Head,
            CreatePresignedUrl(key, HttpMethod.Head, DateTimeOffset.UtcNow.AddMinutes(5)));
        using var response = await clients.CreateClient().SendAsync(
            request,
            HttpCompletionOption.ResponseHeadersRead,
            cancellationToken);
        if (response.StatusCode == System.Net.HttpStatusCode.NotFound) return null;
        response.EnsureSuccessStatusCode();

        return new StoredObjectMetadata(
            response.Content.Headers.ContentType?.MediaType ?? string.Empty,
            response.Content.Headers.ContentLength ?? 0);
    }

    public async Task<StorageDeleteOutcome> DeleteAsync(
        string key,
        CancellationToken cancellationToken = default)
    {
        try
        {
            using var request = new HttpRequestMessage(
                HttpMethod.Delete,
                CreatePresignedUrl(key, HttpMethod.Delete, DateTimeOffset.UtcNow.AddMinutes(5)));
            using var response = await clients.CreateClient().SendAsync(
                request,
                HttpCompletionOption.ResponseHeadersRead,
                cancellationToken);
            if (response.IsSuccessStatusCode || response.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                return StorageDeleteOutcome.AlreadyGone;
            }

            return (int)response.StatusCode is >= 400 and < 500
                ? StorageDeleteOutcome.Permanent
                : StorageDeleteOutcome.Transient;
        }
        catch (HttpRequestException)
        {
            return StorageDeleteOutcome.Transient;
        }
        catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return StorageDeleteOutcome.Transient;
        }
    }

    private string CreatePresignedUrl(
        string key,
        HttpMethod method,
        DateTimeOffset expiresAt,
        string? contentType = null)
    {
        var issuedAt = DateTimeOffset.UtcNow;
        var timestamp = issuedAt.ToString("yyyyMMdd'T'HHmmss'Z'", CultureInfo.InvariantCulture);
        var date = issuedAt.ToString("yyyyMMdd", CultureInfo.InvariantCulture);
        var scope = $"{date}/{Region}/{Service}/aws4_request";
        var host = $"{options.AccountId}.r2.cloudflarestorage.com";
        var canonicalUri = $"/{Uri.EscapeDataString(options.Bucket)}/{EscapeKey(key)}";
        var query = new SortedDictionary<string, string>(StringComparer.Ordinal)
        {
            ["X-Amz-Algorithm"] = Algorithm,
            ["X-Amz-Credential"] = $"{options.AccessKeyId}/{scope}",
            ["X-Amz-Date"] = timestamp,
            ["X-Amz-Expires"] = Math.Max(1, (int)(expiresAt - issuedAt).TotalSeconds)
                .ToString(CultureInfo.InvariantCulture),
            ["X-Amz-SignedHeaders"] = "host"
        };

        var canonicalQuery = string.Join('&', query.Select(pair =>
            $"{Uri.EscapeDataString(pair.Key)}={Uri.EscapeDataString(pair.Value)}"));
        var canonicalHeaders = $"host:{host}\n";
        var canonicalRequest = $"{method.Method}\n{canonicalUri}\n{canonicalQuery}\n{canonicalHeaders}\nhost\nUNSIGNED-PAYLOAD";
        var stringToSign = $"{Algorithm}\n{timestamp}\n{scope}\n{Hash(canonicalRequest)}";
        var signingKey = DeriveSigningKey(date);
        query["X-Amz-Signature"] = Convert.ToHexString(
            HMACSHA256.HashData(signingKey, Encoding.UTF8.GetBytes(stringToSign)))
            .ToLowerInvariant();

        var signedQuery = string.Join('&', query.Select(pair =>
            $"{Uri.EscapeDataString(pair.Key)}={Uri.EscapeDataString(pair.Value)}"));
        return $"https://{host}{canonicalUri}?{signedQuery}";
    }

    private byte[] DeriveSigningKey(string date)
    {
        var dateKey = Hmac($"AWS4{options.SecretAccessKey}", date);
        var regionKey = HMACSHA256.HashData(dateKey, Encoding.UTF8.GetBytes(Region));
        var serviceKey = HMACSHA256.HashData(regionKey, Encoding.UTF8.GetBytes(Service));
        return HMACSHA256.HashData(serviceKey, Encoding.UTF8.GetBytes("aws4_request"));
    }

    private static byte[] Hmac(string key, string value) => HMACSHA256.HashData(
        Encoding.UTF8.GetBytes(key),
        Encoding.UTF8.GetBytes(value));

    private static string EscapeKey(string key) => string.Join(
        '/',
        key.Split('/').Select(Uri.EscapeDataString));

    private static string Hash(string value) => Convert.ToHexString(
        SHA256.HashData(Encoding.UTF8.GetBytes(value)))
        .ToLowerInvariant();
}
