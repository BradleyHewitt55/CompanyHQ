namespace CompanyHQ.Services.Notifications.Contracts.Enums;

/// <summary>Lifecycle state of one persisted notification queue row.</summary>
public enum QueuedNotificationStatus
{
    Pending,
    Sent,
    Failed,
    Abandoned
}
