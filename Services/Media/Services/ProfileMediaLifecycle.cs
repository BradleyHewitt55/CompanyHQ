using CompanyHQ.ServiceCatalog.Contracts;
using CompanyHQ.Media.Contracts.Ports;
using Microsoft.Extensions.Logging;

using CompanyHQ.Media.Contracts.Enums;
using CompanyHQ.Media.Contracts.Models;
using CompanyHQ.Persistence.Services;
using CompanyHQ.Users.Contracts.Ports;
using CompanyHQ.Users.Services;

namespace CompanyHQ.Media.Services;

/// <summary>Retires owner-bound profile uploads while ignoring external and shared image URLs.</summary>
public sealed class ProfileMediaLifecycle(IServiceAvailability? availability,
    IRepository<MediaObject> mediaObjects,
    MediaOptions options,
    IMediaObjectLifecycleStore lifecycle,
    ILogger<ProfileMediaLifecycle> logger) : IProfileMediaLifecycle
{
    /// <summary>Retains the original strict constructor for existing consumers.</summary>
    public ProfileMediaLifecycle(
    IRepository<MediaObject> mediaObjects,
    MediaOptions options,
    IMediaObjectLifecycleStore lifecycle,
    ILogger<ProfileMediaLifecycle> logger) : this(null, mediaObjects, options, lifecycle, logger) { }

    public Task ReleaseUserProfileImageAsync(
        Guid ownerUserId,
        string? previousUrl,
        string? currentUrl,
        CancellationToken cancellationToken = default) {
        availability?.RequireAsync("media", true, cancellationToken).GetAwaiter().GetResult();
        return ReleaseAsync([ownerUserId], MediaScope.User, previousUrl, currentUrl, cancellationToken);
    }

    private async Task ReleaseAsync(
        IReadOnlyCollection<Guid> ownerUserIds,
        MediaScope scope,
        string? previousUrl,
        string? currentUrl,
        CancellationToken cancellationToken)
    {
        var oldUrl = previousUrl?.Trim();
        if (string.IsNullOrWhiteSpace(oldUrl)
            || string.Equals(oldUrl, currentUrl?.Trim(), StringComparison.Ordinal)
            || ownerUserIds.Count == 0)
        {
            return;
        }

        var permittedOwners = ownerUserIds
            .Where(id => id != Guid.Empty)
            .Distinct()
            .ToArray();
        if (permittedOwners.Length == 0) return;

        var candidates = await mediaObjects.Query()
            .Where(media => permittedOwners.Contains(media.OwnerUserId)
                && media.Scope == scope
                && media.Status == MediaStatus.Ready)
            .Select(media => new { media.Id, media.Key })
            .ToListAsync(cancellationToken);
        var matched = candidates.FirstOrDefault(candidate =>
            string.Equals(
                options.GetDurableUrl(candidate.Key, candidate.Id).AbsoluteUri,
                oldUrl,
                StringComparison.Ordinal));
        if (matched is null) return;

        if (!await lifecycle.TryMarkDeletedAsync(matched.Id, MediaStatus.Ready, cancellationToken))
        {
            logger.LogWarning(
                "Profile media {MediaObjectId} changed state before it could be retired; cleanup was not scheduled.",
                matched.Id);
        }
    }
}
