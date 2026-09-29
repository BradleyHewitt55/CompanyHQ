using CompanyHQ.Services.Notifications.Contracts.Models;

namespace CompanyHQ.Services.Notifications.Services;

internal interface INotificationDeliveryService
{
    Task<NotificationDeliveryResult> DeliverAsync(
        QueuedNotification notification,
        CancellationToken cancellationToken = default);
}
