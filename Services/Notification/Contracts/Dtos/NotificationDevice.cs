using CompanyHQ.Services.Notifications.Contracts.Enums;

namespace CompanyHQ.Services.Notifications.Contracts.Dtos;

/// <summary>A device summary safe to show to an administrator; it deliberately has no token.</summary>
public sealed record NotificationDeviceDto(
    DevicePlatform Platform,
    DateTimeOffset LastSeenAt,
    bool IsEnabled);
