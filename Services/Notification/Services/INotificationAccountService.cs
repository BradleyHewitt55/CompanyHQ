using CompanyHQ.Notifications.Contracts.Dtos;
using CompanyHQ.Notifications.Contracts.Enums;
using CompanyHQ.Notifications.Contracts.Forms;

namespace CompanyHQ.Notifications.Services;

/// <summary>Owns authenticated device/preferences writes and read-only admin summaries.</summary>
public interface INotificationAccountService
{
    Task RegisterDeviceAsync(
        Guid userId,
        RegisterDeviceRequest request,
        CancellationToken cancellationToken = default);

    Task RemoveDeviceAsync(
        Guid userId,
        string token,
        CancellationToken cancellationToken = default);

    Task<NotificationPreferencesDto> GetPreferencesAsync(
        Guid userId,
        CancellationToken cancellationToken = default);

    Task<NotificationPreferencesDto> UpdatePreferencesAsync(
        Guid userId,
        UpdateNotificationPreferencesRequest request,
        CancellationToken cancellationToken = default);

    Task<AdminNotificationUserSnapshotDto> GetUserSnapshotAsync(
        Guid userId,
        CancellationToken cancellationToken = default);

    Task<AdminNotificationQueueHealthDto> GetQueueHealthAsync(
        CancellationToken cancellationToken = default);
}
