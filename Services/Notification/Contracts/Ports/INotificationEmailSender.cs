namespace CompanyHQ.Notifications.Contracts.Ports;

/// <summary>
/// The email channel. Optional: with no adapter plugged in, the email channel is skipped, not
/// failed. The host bridges it to whichever email service is plugged in; Notification never
/// references that service.
/// </summary>
public interface INotificationEmailSender
{
    /// <summary>The same <paramref name="idempotencyKey"/> queues once.</summary>
    Task<NotificationEmailOutcome> SendAsync(
        string recipient,
        string kind,
        string title,
        string body,
        string idempotencyKey,
        CancellationToken cancellationToken = default);
}

/// <summary>What happened to one notification email.</summary>
public enum NotificationEmailOutcome
{
    Queued,
    RetryableFailure,
    Rejected
}
