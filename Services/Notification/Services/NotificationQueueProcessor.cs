using CompanyHQ.ServiceCatalog.Contracts;
using CompanyHQ.Notifications.Contracts.Enums;
using CompanyHQ.Notifications.Contracts.Models;
using CompanyHQ.Persistence.Services;

namespace CompanyHQ.Notifications.Services;

/// <summary>
/// Claims one due batch of <c>queued_notification</c> and delivers it. Scoped and resolved per batch
/// by <see cref="NotificationWorker"/>, so every dependency is constructor-injected and checked by the
/// host's build-time validation. Several API instances can run it: a claim is row-locked and leased.
/// </summary>
internal sealed class NotificationQueueProcessor(
    IPersistence context,
    INotificationDeliveryService delivery,
    TimeProvider timeProvider,
    IServiceAvailability? availability = null)
{
    private const int BatchSize = 50;
    private const int MaxAttempts = 5;
    private static readonly TimeSpan ClaimLease = TimeSpan.FromHours(1);

    public async Task<int> ProcessBatchAsync(CancellationToken cancellationToken)
    {
        if (availability is not null && (await availability.GetAsync("notifications", cancellationToken))?.Status != ServiceStatus.EnabledReady)
            return 0;
        var now = timeProvider.GetUtcNow();

        // The claim is one transaction. Rows another worker has already locked are skipped rather
        // than waited for, so two workers never deliver the same notification.
        var notifications = await context.InTransactionAsync(async _ =>
        {
            var claimed = await context.Locked<QueuedNotification>(skipLocked: true)
                .Where(notification => notification.Status == QueuedNotificationStatus.Pending && notification.NextAttemptAt <= now)
                .OrderBy(notification => notification.NextAttemptAt)
                .ThenBy(notification => notification.Id)
                .Take(BatchSize)
                .ToListAsync(cancellationToken);

            // The lease keeps a committed claim invisible to another worker while provider calls
            // run. An expired lease makes a crashed worker's rows eligible again.
            var leaseUntil = now.Add(ClaimLease);
            foreach (var notification in claimed)
            {
                notification.NextAttemptAt = leaseUntil;
            }

            await context.SaveChangesAsync(cancellationToken);
            return claimed;
        }, cancellationToken: cancellationToken);

        foreach (var notification in notifications)
        {
            NotificationDeliveryResult result;
            try
            {
                if (availability is not null) await availability.RequireAsync("notifications", true, cancellationToken);
                result = await delivery.DeliverAsync(notification, cancellationToken);
            }
            catch (ServiceUnavailableException)
            {
                result = new(NotificationDeliveryDisposition.Deferred);
            }
            switch (result.Disposition)
            {
                case NotificationDeliveryDisposition.Deferred:
                    notification.NextAttemptAt = timeProvider.GetUtcNow().AddSeconds(1);
                    break;
                case NotificationDeliveryDisposition.Sent:
                    notification.Status = QueuedNotificationStatus.Sent;
                    notification.LastError = null;
                    break;
                case NotificationDeliveryDisposition.TransientFailure:
                    MarkTransientFailure(
                        notification,
                        timeProvider.GetUtcNow(),
                        result.Error ?? "Notification delivery was temporarily unavailable.");
                    break;
                case NotificationDeliveryDisposition.PermanentFailure:
                    notification.Attempts++;
                    notification.Status = QueuedNotificationStatus.Failed;
                    notification.LastError = result.Error ?? "Notification delivery was rejected.";
                    break;
            }
        }

        // Deliberately after all provider calls and outside the claim transaction, so a slow
        // provider never holds row locks or an idle transaction.
        await context.SaveChangesAsync(cancellationToken);
        return notifications.Count;
    }

    internal static void MarkTransientFailure(
        QueuedNotification notification,
        DateTimeOffset now,
        string error)
    {
        notification.Attempts++;
        notification.LastError = error;

        if (notification.Attempts >= MaxAttempts)
        {
            notification.Status = QueuedNotificationStatus.Abandoned;
            return;
        }

        notification.Status = QueuedNotificationStatus.Pending;
        var seconds = Math.Min(300, Math.Pow(2, notification.Attempts - 1));
        notification.NextAttemptAt = now.AddSeconds(seconds);
    }
}
