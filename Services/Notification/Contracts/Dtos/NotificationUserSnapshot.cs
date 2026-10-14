namespace CompanyHQ.Notifications.Contracts.Dtos;

/// <summary>Read-only notification state shown on an administrator's user detail page.</summary>
public sealed record AdminNotificationUserSnapshotDto(
    IReadOnlyList<NotificationDeviceDto> Devices,
    NotificationPreferencesDto Preferences);
