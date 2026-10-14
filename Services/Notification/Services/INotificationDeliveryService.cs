using CompanyHQ.Notifications.Contracts.Models;

namespace CompanyHQ.Notifications.Services;

internal interface INotificationDeliveryService
{
    Task<NotificationDeliveryResult> DeliverAsync(
        QueuedNotification notification,
        CancellationToken cancellationToken = default);
}
