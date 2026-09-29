using Microsoft.EntityFrameworkCore;
using CompanyHQ.Services.Notifications.Contracts.Dtos;
using CompanyHQ.Services.Notifications.Contracts.Enums;
using CompanyHQ.Services.Notifications.Contracts.Forms;
using CompanyHQ.Services.Notifications.Contracts.Models;
using CompanyHQ.Services.Postgres.Data;

namespace CompanyHQ.Services.Notifications.Services;

/// <inheritdoc cref="INotificationAccountService" />
public sealed class NotificationAccountService(
    PostgresDbContext context,
    TimeProvider timeProvider) : INotificationAccountService
{
    public async Task RegisterDeviceAsync(
        Guid userId,
        RegisterDeviceRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ValidateUserId(userId);

        ArgumentException.ThrowIfNullOrWhiteSpace(request.Token);
        if (!Enum.IsDefined(request.Platform))
        {
            throw new ArgumentException("The device platform is not supported.", nameof(request));
        }

        var token = request.Token.Trim();
        if (token.Length > 4096)
        {
            throw new ArgumentException("A device token is required and must be at most 4096 characters.", nameof(request));
        }

        var device = await context.Set<PushDevice>()
            .SingleOrDefaultAsync(item => item.Token == token, cancellationToken);
        var now = timeProvider.GetUtcNow();

        if (device is null)
        {
            await context.Set<PushDevice>().AddAsync(new PushDevice
            {
                UserId = userId,
                Token = token,
                Platform = request.Platform,
                LastSeenAt = now
            }, cancellationToken);
        }
        else
        {
            // A token can only belong to one account at a time. This also repairs a shared device
            // after the previous account failed to reach the logout endpoint.
            device.UserId = userId;
            device.Platform = request.Platform;
            device.LastSeenAt = now;
            device.DisabledAt = null;
        }

        await context.SaveChangesAsync(cancellationToken);
    }

    public Task RemoveDeviceAsync(
        Guid userId,
        string token,
        CancellationToken cancellationToken = default)
    {
        ValidateUserId(userId);
        ArgumentException.ThrowIfNullOrWhiteSpace(token);

        if (token.Trim().Length > 4096)
        {
            throw new ArgumentException("A device token must be at most 4096 characters.", nameof(token));
        }

        return context.Set<PushDevice>()
            .Where(device => device.UserId == userId && device.Token == token.Trim())
            .ExecuteDeleteAsync(cancellationToken);
    }

    public async Task<NotificationPreferencesDto> GetPreferencesAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        ValidateUserId(userId);

        var preference = await context.Set<NotificationPreference>()
            .AsNoTracking()
            .SingleOrDefaultAsync(item => item.UserId == userId, cancellationToken);

        return ToDto(preference);
    }

    public async Task<NotificationPreferencesDto> UpdatePreferencesAsync(
        Guid userId,
        UpdateNotificationPreferencesRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ValidateUserId(userId);

        var preference = await context.Set<NotificationPreference>()
            .SingleOrDefaultAsync(item => item.UserId == userId, cancellationToken);

        if (request.EmailEnabled && request.PushEnabled)
        {
            if (preference is not null)
            {
                context.Set<NotificationPreference>().Remove(preference);
                await context.SaveChangesAsync(cancellationToken);
            }

            return new NotificationPreferencesDto(true, true);
        }

        if (preference is null)
        {
            preference = new NotificationPreference
            {
                UserId = userId
            };
            await context.Set<NotificationPreference>().AddAsync(preference, cancellationToken);
        }

        preference.EmailEnabled = request.EmailEnabled;
        preference.PushEnabled = request.PushEnabled;
        await context.SaveChangesAsync(cancellationToken);

        return ToDto(preference);
    }

    public async Task<AdminNotificationUserSnapshotDto> GetUserSnapshotAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        ValidateUserId(userId);

        var devices = await context.Set<PushDevice>()
            .AsNoTracking()
            .Where(device => device.UserId == userId)
            .OrderByDescending(device => device.LastSeenAt)
            .Select(device => new NotificationDeviceDto(
                device.Platform,
                device.LastSeenAt,
                device.DisabledAt == null))
            .ToListAsync(cancellationToken);
        var preferences = await GetPreferencesAsync(userId, cancellationToken);

        return new AdminNotificationUserSnapshotDto(devices, preferences);
    }

    public async Task<AdminNotificationQueueHealthDto> GetQueueHealthAsync(
        CancellationToken cancellationToken = default)
    {
        var counts = await context.Set<QueuedNotification>()
            .AsNoTracking()
            .Where(notification => notification.Status == QueuedNotificationStatus.Pending
                || notification.Status == QueuedNotificationStatus.Failed
                || notification.Status == QueuedNotificationStatus.Abandoned)
            .GroupBy(notification => notification.Status)
            .Select(group => new { group.Key, Count = group.Count() })
            .ToListAsync(cancellationToken);

        var countByStatus = counts.ToDictionary(item => item.Key, item => item.Count);
        return new AdminNotificationQueueHealthDto(
            countByStatus.GetValueOrDefault(QueuedNotificationStatus.Pending),
            countByStatus.GetValueOrDefault(QueuedNotificationStatus.Failed),
            countByStatus.GetValueOrDefault(QueuedNotificationStatus.Abandoned));
    }

    private static NotificationPreferencesDto ToDto(NotificationPreference? preference) =>
        preference is null
            ? new NotificationPreferencesDto(true, true)
            : new NotificationPreferencesDto(preference.EmailEnabled, preference.PushEnabled);

    private static void ValidateUserId(Guid userId)
    {
        if (userId == Guid.Empty)
        {
            throw new ArgumentException("A user id is required.", nameof(userId));
        }
    }
}
