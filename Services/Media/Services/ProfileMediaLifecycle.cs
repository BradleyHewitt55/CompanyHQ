using CompanyHQ.Services.Media.Contracts.Ports;
using Microsoft.Extensions.Logging;

using CompanyHQ.Services.Media.Contracts.Enums;
using CompanyHQ.Services.Media.Contracts.Models;
using CompanyHQ.Services.Persistence.Services;
using CompanyHQ.Services.Users.Contracts.Ports;
using CompanyHQ.Services.Users.Services;

namespace CompanyHQ.Services.Media.Services;

/// <summary>Retires owner-bound profile uploads while ignoring external and shared image URLs.</summary>
public sealed class ProfileMediaLifecycle(
    IRepository<MediaObject> mediaObjects,
    MediaOptions options,
    IMediaObjectLifecycleStore lifecycle,
    ILogger<ProfileMediaLifecycle> logger) : IProfileMediaLifecycle
{
    public Task ReleaseUserProfileImageAsync(
        Guid ownerUserId,
        string? previousUrl,
        string? currentUrl,
        CancellationToken cancellationToken = default) =>
        ReleaseAsync([ownerUserId], MediaScope.User, previousUrl, currentUrl, cancellationToken);

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
