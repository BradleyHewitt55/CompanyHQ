namespace CompanyHQ.Services.Notifications.Services;

internal enum NotificationDeliveryDisposition
{
    Sent,
    TransientFailure,
    PermanentFailure
}

internal sealed record NotificationDeliveryResult(
    NotificationDeliveryDisposition Disposition,
    string? Error = null);
