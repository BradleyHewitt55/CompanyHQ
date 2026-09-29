using CompanyHQ.Services.Media.Contracts.Ports;
using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;

using CompanyHQ.Services.Media.Contracts.Dtos;
using CompanyHQ.Services.Media.Contracts.Enums;
using CompanyHQ.Services.Media.Contracts.Forms;
using CompanyHQ.Services.Media.Contracts.Models;
using CompanyHQ.Services.Postgres.Contracts.Enums;
using CompanyHQ.Services.Postgres.Services;

namespace CompanyHQ.Services.Media.Services;

/// <inheritdoc cref="IMediaService" />
public sealed class MediaService(
    IPostgresService<MediaObject> mediaObjects,
    IMediaObjectStorageProvider storage,
    MediaOptions options,
    IMediaAccessPolicyResolver accessPolicies,
    IMediaObjectLifecycleStore lifecycle) : IMediaService
{
    private static readonly IReadOnlyDictionary<string, string> Extensions = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        ["image/jpeg"] = "jpg",
        ["image/png"] = "png",
        ["image/webp"] = "webp",
        ["video/mp4"] = "mp4",
        ["video/webm"] = "webm",
        ["video/quicktime"] = "mov",
        ["video/x-m4v"] = "m4v"
    };

    public async Task<MediaUploadTicketDto> CreateUploadAsync(
        CreateMediaUploadRequest request,
        Guid ownerUserId,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (ownerUserId == Guid.Empty) throw new ArgumentException("An upload owner is required.", nameof(ownerUserId));
        var scope = request.Scope ?? throw new ArgumentException("A media scope is required.", nameof(request));
        if (string.IsNullOrWhiteSpace(request.ContentType)) throw new ArgumentException("A content type is required.", nameof(request));
        var contentType = NormaliseContentType(request.ContentType);
        ValidateRequest(scope, contentType, request.SizeBytes);

        var media = new MediaObject
        {
            OwnerUserId = ownerUserId,
            Key = string.Empty,
            ContentType = contentType,
            SizeBytes = request.SizeBytes,
            Scope = scope,
            Status = MediaStatus.Pending
        };
        media.Key = $"{scope.ToString().ToLowerInvariant()}/{ownerUserId:N}/{media.Id:N}.{Extensions[contentType]}";
        await mediaObjects.AddAsync(media, cancellationToken);

        var expiresAt = DateTimeOffset.UtcNow.Add(options.UploadUrlLifetime);
        var uploadUrl = storage.CreateUploadUrl(media.Key, contentType, expiresAt);
        var url = IsPublicScope(scope) ? options.GetDurableUrl(media.Key, media.Id).AbsoluteUri : null;
        return new MediaUploadTicketDto(media.Id, uploadUrl, url, expiresAt);
    }

    public async Task<MediaOperationResult> UploadAsync(
        Guid mediaObjectId,
        Guid uploaderUserId,
        Stream content,
        string? contentType,
        long? contentLength,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(content);
        if (uploaderUserId == Guid.Empty) return new(MediaOperationError.Forbidden);
        var media = await mediaObjects.GetAsync(mediaObjectId, cancellationToken);
        if (media is null) return new(MediaOperationError.NotFound);
        if (media.OwnerUserId != uploaderUserId) return new(MediaOperationError.Forbidden);
        if (media.Status is not MediaStatus.Pending) return new(MediaOperationError.NotPending);

        // The ticket already fixed the type and size; the body has to be the upload that was asked
        // for, or Complete would reject it after the bytes were already paid for.
        if (NormaliseOptionalContentType(contentType) != media.ContentType
            || contentLength is not { } length
            || length != media.SizeBytes
            || length > MaxUploadBytesFor(media.ContentType))
        {
            return new(MediaOperationError.MetadataMismatch);
        }

        return await storage.PutAsync(media.Key, media.ContentType, content, length, cancellationToken)
            ? MediaOperationResult.Success()
            : new(MediaOperationError.UploadFailed);
    }

    public async Task<MediaOperationResult> CompleteAsync(
        Guid mediaObjectId,
        Guid uploaderUserId,
        CancellationToken cancellationToken = default)
    {
        if (uploaderUserId == Guid.Empty) return new(MediaOperationError.Forbidden);
        var media = await mediaObjects.GetAsync(mediaObjectId, cancellationToken);
        if (media is null) return new(MediaOperationError.NotFound);
        if (media.OwnerUserId != uploaderUserId) return new(MediaOperationError.Forbidden);
        if (media.Status is not MediaStatus.Pending) return new(MediaOperationError.NotPending);

        var metadata = await storage.GetMetadataAsync(media.Key, cancellationToken);
        if (metadata is null) return new(MediaOperationError.ObjectMissing);

        var contentType = NormaliseOptionalContentType(metadata.ContentType);
        if (contentType is null
            || !IsAllowedContentType(contentType)
            || contentType != media.ContentType
            || metadata.SizeBytes != media.SizeBytes
            || metadata.SizeBytes > MaxUploadBytesFor(media.ContentType))
        {
            return new(MediaOperationError.MetadataMismatch);
        }

        return await lifecycle.TryMarkReadyAsync(media.Id, MediaStatus.Pending, cancellationToken)
            ? MediaOperationResult.Success()
            : new(MediaOperationError.NotPending);
    }

    public async Task<ClientMediaObjectDto?> GetAsync(
        Guid mediaObjectId,
        Guid viewerUserId,
        ResponseAudience audience = ResponseAudience.Client,
        CancellationToken cancellationToken = default)
    {
        if (viewerUserId == Guid.Empty) return null;
        var projected = await mediaObjects.Query()
            .Where(media => media.Id == mediaObjectId && media.Status == MediaStatus.Ready)
            .Select(ProjectionFor(audience))
            .FirstOrDefaultAsync(cancellationToken);
        if (projected is null) return null;

        if (IsPublicScope(projected.Dto.Scope))
        {
            return projected.Dto with { DownloadUrl = options.GetDurableUrl(projected.Key, projected.Dto.Id).AbsoluteUri };
        }

        if (!await accessPolicies.CanReadAsync(projected.Dto.Scope, projected.Dto.Id, viewerUserId, cancellationToken)) return null;
        var downloadUrl = storage.CreateDownloadUrl(projected.Key, DateTimeOffset.UtcNow.Add(options.DownloadUrlLifetime));
        return projected.Dto with { DownloadUrl = downloadUrl };
    }

    /// <summary>
    /// Where a browser should actually fetch a ready public object from. Returns a short-lived
    /// signed storage URL for the caller to be redirected to — the bytes still travel straight from storage to
    /// the client, so this is a signing step, not a byte proxy. Null covers "not ready", "not a
    /// public scope", and "no such object" alike: none of them is a distinction a caller may act on.
    /// </summary>
    public async Task<Uri?> GetPublicContentUrlAsync(Guid mediaObjectId, CancellationToken cancellationToken = default)
    {
        var media = await mediaObjects.GetAsync(mediaObjectId, cancellationToken);
        if (media is not { Status: MediaStatus.Ready } || !IsPublicScope(media.Scope)) return null;

        return new Uri(
            storage.CreateDownloadUrl(media.Key, DateTimeOffset.UtcNow.Add(options.DownloadUrlLifetime)),
            UriKind.Absolute);
    }

    private static bool IsPublicScope(MediaScope scope) =>
        scope is MediaScope.User;

    public async Task<MediaOperationResult> DeleteAsync(
        Guid mediaObjectId,
        Guid actingUserId,
        bool administrator = false,
        CancellationToken cancellationToken = default)
    {
        if (actingUserId == Guid.Empty) return new(MediaOperationError.Forbidden);
        var media = await mediaObjects.GetAsync(mediaObjectId, cancellationToken);
        if (media is null) return new(MediaOperationError.NotFound);
        if (!administrator && media.OwnerUserId != actingUserId) return new(MediaOperationError.Forbidden);
        if (media.Status is not MediaStatus.Deleted
            && !await lifecycle.TryMarkDeletedAsync(media.Id, media.Status, cancellationToken))
        {
            return new(MediaOperationError.NotPending);
        }

        return MediaOperationResult.Success();
    }

    private void ValidateRequest(MediaScope scope, string contentType, long sizeBytes)
    {
        if (!Enum.IsDefined(scope)) throw new ArgumentException("The media scope is not defined.", nameof(scope));
        if (!contentType.StartsWith("image/", StringComparison.Ordinal)
            && !contentType.StartsWith("video/", StringComparison.Ordinal))
        {
            throw new ArgumentException("Only image and video media are supported.", nameof(contentType));
        }
        if (contentType.StartsWith("video/", StringComparison.Ordinal)
            && scope is not MediaScope.Message)
        {
            throw new ArgumentException("Video uploads are supported for message attachments only.", nameof(scope));
        }
        if (!IsAllowedContentType(contentType)) throw new ArgumentException("The content type is not allowed.", nameof(contentType));
        if (!Extensions.ContainsKey(contentType)) throw new InvalidOperationException($"No object-key extension is defined for {contentType}.");
        var maxBytes = contentType.StartsWith("video/", StringComparison.Ordinal)
            ? options.MaxVideoUploadBytes
            : options.MaxUploadBytes;
        if (sizeBytes is <= 0 || sizeBytes > maxBytes)
        {
            throw new ArgumentOutOfRangeException(nameof(sizeBytes), sizeBytes, $"The upload must be between 1 and {maxBytes} bytes.");
        }
    }

    private bool IsAllowedContentType(string contentType) => options.AllowedContentTypes.Any(allowed =>
        string.Equals(NormaliseContentType(allowed), contentType, StringComparison.OrdinalIgnoreCase));

    private long MaxUploadBytesFor(string contentType) =>
        contentType.StartsWith("video/", StringComparison.Ordinal)
            ? options.MaxVideoUploadBytes
            : options.MaxUploadBytes;

    private static string NormaliseContentType(string contentType) => contentType.Trim().Split(';', 2)[0].Trim().ToLowerInvariant();

    private static string? NormaliseOptionalContentType(string? contentType) =>
        string.IsNullOrWhiteSpace(contentType) ? null : NormaliseContentType(contentType);

    private static Expression<Func<MediaObject, MediaReadProjection>> ProjectionFor(ResponseAudience audience) =>
        audience is ResponseAudience.Admin ? AdminProjection : ClientProjection;

    private static Expression<Func<MediaObject, MediaReadProjection>> ClientProjection { get; } = media => new(
        new ClientMediaObjectDto(media.Id, media.Scope, media.ContentType, media.SizeBytes, media.Status, string.Empty),
        media.Key,
        media.OwnerUserId);

    private static Expression<Func<MediaObject, MediaReadProjection>> AdminProjection { get; } = media => new(
        (ClientMediaObjectDto)new AdminMediaObjectDto(
            media.Id, media.Scope, media.ContentType, media.SizeBytes, media.Status, string.Empty,
            media.OwnerUserId, media.Key, media.UpdatedAt),
        media.Key,
        media.OwnerUserId);

    private sealed record MediaReadProjection(ClientMediaObjectDto Dto, string Key, Guid OwnerUserId);
}
