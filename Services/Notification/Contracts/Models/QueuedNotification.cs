using CompanyHQ.Notifications.Contracts.Enums;
using CompanyHQ.Persistence.Contracts.Models;

namespace CompanyHQ.Notifications.Contracts.Models;

/// <summary>A durable delivery attempt owned by the notification worker.</summary>
public sealed class QueuedNotification : BaseModel
{
    public Guid RecipientUserId { get; set; }

    public required string Kind { get; set; }

    public required string Title { get; set; }

    public required string Body { get; set; }

    public string? DataJson { get; set; }

    /// <summary>The caller's channel restriction, persisted so retries cannot widen delivery.</summary>
    public NotificationChannels Channels { get; set; } = NotificationChannels.All;

    public QueuedNotificationStatus Status { get; set; } = QueuedNotificationStatus.Pending;

    public int Attempts { get; set; }

    public DateTimeOffset NextAttemptAt { get; set; }

    public string? LastError { get; set; }
}
