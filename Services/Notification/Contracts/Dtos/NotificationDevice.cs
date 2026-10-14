using CompanyHQ.Notifications.Contracts.Enums;

namespace CompanyHQ.Notifications.Contracts.Dtos;

/// <summary>A device summary safe to show to an administrator; it deliberately has no token.</summary>
public sealed record NotificationDeviceDto(
    DevicePlatform Platform,
    DateTimeOffset LastSeenAt,
    bool IsEnabled);
