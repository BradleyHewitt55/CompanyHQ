namespace CompanyHQ.Messages.Contracts.Ports;

/// <summary>
/// Queues a push notification about a conversation. Optional: with no adapter plugged in, nothing is
/// sent.
/// </summary>
/// <remarks>
/// An adapter must add to the caller's unit of work and not save: Message's own
/// <c>SaveChangesAsync</c> commits the queued rows with the change that caused them.
/// </remarks>
public interface IMessageNotifier
{
    Task EnqueueAsync(MessageNotification notification, CancellationToken cancellationToken = default);

    Task EnqueueManyAsync(
        IEnumerable<MessageNotification> notifications,
        CancellationToken cancellationToken = default);
}
