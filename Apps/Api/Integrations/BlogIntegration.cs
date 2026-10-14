using CompanyHQ.Blogs.Contracts.Ports;
using CompanyHQ.Media.Contracts.Enums;
using CompanyHQ.Media.Contracts.Models;
using CompanyHQ.Media.Services;
using CompanyHQ.Persistence.Services;

namespace Api.Integrations;

internal sealed class BlogCoverImages(
    IMediaService media,
    IRepository<MediaObject> objects,
    IMediaObjectLifecycleStore lifecycle,
    ILogger<BlogCoverImages> logger) : IBlogCoverImages
{
    public async Task<string?> GetReadyUrlAsync(Guid mediaObjectId, Guid actingUserId, CancellationToken cancellationToken = default)
    {
        if (!await objects.Query().AnyAsync(item => item.Id == mediaObjectId
                && item.OwnerUserId == actingUserId && item.Scope == MediaScope.Content
                && item.Status == MediaStatus.Ready && item.ContentType.StartsWith("image/"), cancellationToken))
            return null;
        return (await media.GetAsync(mediaObjectId, actingUserId, cancellationToken: cancellationToken))?.DownloadUrl;
    }

    public async Task RetireAsync(Guid mediaObjectId, CancellationToken cancellationToken = default)
    {
        try
        {
            var item = await objects.GetAsync(mediaObjectId, cancellationToken);
            if (item is { Scope: MediaScope.Content, Status: MediaStatus.Ready })
                await lifecycle.TryMarkDeletedAsync(item.Id, item.Status, cancellationToken);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            logger.LogWarning("Cover {MediaObjectId} was replaced but cleanup could not be queued ({ExceptionType}).", mediaObjectId, exception.GetType().Name);
        }
    }
}
