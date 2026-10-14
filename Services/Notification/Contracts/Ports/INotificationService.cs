namespace CompanyHQ.Notifications.Contracts.Ports;

/// <summary>
/// Durable, fire-and-forget notification enqueueing for other services. Callers reference this
/// Contracts assembly only and take it as an optional dependency, so they still work when
/// Notification is not plugged in.
/// </summary>
/// <remarks>
/// Rows are added to the caller's scoped context and are not saved here: the caller's own
/// <c>SaveChangesAsync</c> commits them with the change that caused them.
/// </remarks>
public interface INotificationService
{
    Task EnqueueAsync(NotificationRequest request, CancellationToken cancellationToken = default);

    Task EnqueueManyAsync(
        IEnumerable<NotificationRequest> requests,
        CancellationToken cancellationToken = default);
}
