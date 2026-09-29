using CompanyHQ.Services.Media.Contracts.Dtos;
using CompanyHQ.Services.Media.Contracts.Forms;
using CompanyHQ.Services.Postgres.Contracts.Enums;

namespace CompanyHQ.Services.Media.Services;

/// <summary>Direct-upload lifecycle, policy-gated reads, and media deletion.</summary>
public interface IMediaService
{
    Task<MediaUploadTicketDto> CreateUploadAsync(
        CreateMediaUploadRequest request,
        Guid ownerUserId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Streams the bytes of a pending upload to storage on the caller's behalf. The alternative is a
    /// browser PUT straight to the presigned URL, which needs a bucket CORS rule per origin.
    /// </summary>
    Task<MediaOperationResult> UploadAsync(
        Guid mediaObjectId,
        Guid uploaderUserId,
        Stream content,
        string? contentType,
        long? contentLength,
        CancellationToken cancellationToken = default);

    Task<MediaOperationResult> CompleteAsync(
        Guid mediaObjectId,
        Guid uploaderUserId,
        CancellationToken cancellationToken = default);

    Task<ClientMediaObjectDto?> GetAsync(
        Guid mediaObjectId,
        Guid viewerUserId,
        ResponseAudience audience = ResponseAudience.Client,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// The signed location a browser is redirected to for a ready public (User) object. Null
    /// when the object is missing, not ready, or not a public scope.
    /// </summary>
    Task<Uri?> GetPublicContentUrlAsync(
        Guid mediaObjectId,
        CancellationToken cancellationToken = default);

    Task<MediaOperationResult> DeleteAsync(
        Guid mediaObjectId,
        Guid actingUserId,
        bool administrator = false,
        CancellationToken cancellationToken = default);
}
