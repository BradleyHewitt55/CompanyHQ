using CompanyHQ.Notifications.Contracts.Enums;

namespace CompanyHQ.Notifications.Contracts.Ports;

/// <summary>Opaque delivery input supplied by a domain service.</summary>
public sealed record NotificationRequest(
    Guid RecipientUserId,
    string Kind,
    string Title,
    string Body,
    IReadOnlyDictionary<string, string>? Data = null,
    NotificationChannels Channels = NotificationChannels.All);
