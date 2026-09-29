namespace CompanyHQ.Services.Notifications.Contracts.Enums;

/// <summary>Delivery channels a caller may request for one queued notification.</summary>
[Flags]
public enum NotificationChannels
{
    None = 0,
    Push = 1,
    Email = 2,
    All = Push | Email
}
