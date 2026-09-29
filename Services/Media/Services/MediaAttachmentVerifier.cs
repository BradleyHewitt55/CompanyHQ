using Microsoft.EntityFrameworkCore;

using CompanyHQ.Services.Media.Contracts.Enums;
using CompanyHQ.Services.Media.Contracts.Models;
using CompanyHQ.Services.Media.Contracts.Ports;
using CompanyHQ.Services.Postgres.Services;

namespace CompanyHQ.Services.Media.Services;

/// <inheritdoc cref="IMediaAttachmentVerifier" />
internal sealed class MediaAttachmentVerifier(IPostgresService<MediaObject> mediaObjects) : IMediaAttachmentVerifier
{
    public Task<bool> IsReadyAsync(
        Guid mediaObjectId,
        Guid ownerUserId,
        MediaScope scope,
        CancellationToken cancellationToken = default) =>
        mediaObjects.Query().AnyAsync(
            media => media.Id == mediaObjectId
                && media.OwnerUserId == ownerUserId
                && media.Scope == scope
                && media.Status == MediaStatus.Ready,
            cancellationToken);
}
