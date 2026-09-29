using Microsoft.EntityFrameworkCore;
using CompanyHQ.Services.Notifications.Contracts.Enums;
using CompanyHQ.Services.Notifications.Contracts.Models;
using CompanyHQ.Services.Postgres.Data;

namespace CompanyHQ.Services.Notifications.Services;

/// <summary>
/// Claims one due batch of <c>queued_notification</c> and delivers it. Scoped and resolved per batch
/// by <see cref="NotificationWorker"/>, so every dependency is constructor-injected and checked by the
/// host's build-time validation. Several API instances can run it: a claim is row-locked and leased.
/// </summary>
internal sealed class NotificationQueueProcessor(
    PostgresDbContext context,
    INotificationDeliveryService delivery,
    TimeProvider timeProvider)
{
    private const int BatchSize = 50;
    private const int MaxAttempts = 5;
    private static readonly TimeSpan ClaimLease = TimeSpan.FromHours(1);

    public async Task<int> ProcessBatchAsync(CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow();

        // The retrying execution strategy refuses a user-initiated transaction unless the whole
        // claim is retriable, so the transaction opens inside the strategy rather than around it.
        var notifications = await context.Database.CreateExecutionStrategy().ExecuteAsync(async () =>
        {
            // A retry must not reuse entities tracked by the failed attempt.
            context.ChangeTracker.Clear();

            await using var transaction = await context.Database.BeginTransactionAsync(cancellationToken);

            var claimed = await context.Set<QueuedNotification>()
                .FromSqlInterpolated($"""
                    SELECT *
                    FROM queued_notification
                    WHERE status = {nameof(QueuedNotificationStatus.Pending)}
                      AND next_attempt_at <= {now}
                    ORDER BY next_attempt_at, id
                    LIMIT {BatchSize}
                    FOR UPDATE SKIP LOCKED
                    """)
                .ToListAsync(cancellationToken);

            // The lease keeps a committed claim invisible to another worker while provider calls
            // run. An expired lease makes a crashed worker's rows eligible again.
            var leaseUntil = now.Add(ClaimLease);
            foreach (var notification in claimed)
            {
                notification.NextAttemptAt = leaseUntil;
            }

            await context.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return claimed;
        });

        foreach (var notification in notifications)
        {
            var result = await delivery.DeliverAsync(notification, cancellationToken);
            switch (result.Disposition)
            {
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
