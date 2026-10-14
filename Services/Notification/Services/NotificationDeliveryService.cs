using System.Text.Json;
using Microsoft.Extensions.Logging;
using CompanyHQ.Notifications.Contracts.Enums;
using CompanyHQ.Notifications.Contracts.Models;
using CompanyHQ.Notifications.Contracts.Ports;
using CompanyHQ.Persistence.Services;
using CompanyHQ.Users.Contracts.Models;

namespace CompanyHQ.Notifications.Services;

/// <summary>
/// Delivers one claimed notification over the channels the recipient allows. Each channel is an
/// optional port: with no email or push adapter plugged in, that channel is skipped, not failed.
/// </summary>
internal sealed class NotificationDeliveryService(
    IPersistence context,
    TimeProvider timeProvider,
    ILogger<NotificationDeliveryService> logger,
    INotificationEmailSender? email = null,
    IPushSender? push = null) : INotificationDeliveryService
{
    public async Task<NotificationDeliveryResult> DeliverAsync(
        QueuedNotification notification,
        CancellationToken cancellationToken = default)
    {
        var preference = await context.Query<NotificationPreference>()
            .SingleOrDefaultAsync(
                item => item.UserId == notification.RecipientUserId,
                cancellationToken);

        var channels = EffectiveChannels(notification, preference);
        if (channels == NotificationChannels.None)
        {
            return Sent();
        }

        var outcomes = new List<NotificationDeliveryResult>(2);

        if ((channels & NotificationChannels.Email) != 0)
        {
            outcomes.Add(await DeliverEmailAsync(notification, cancellationToken));
        }

        if ((channels & NotificationChannels.Push) != 0)
        {
            outcomes.Add(await DeliverPushAsync(notification, cancellationToken));
        }

        return Combine(outcomes);
    }

    internal static NotificationChannels EffectiveChannels(
        QueuedNotification notification,
        NotificationPreference? preference)
    {
        var enabled = (preference?.EmailEnabled != false ? NotificationChannels.Email : NotificationChannels.None)
            | (preference?.PushEnabled != false ? NotificationChannels.Push : NotificationChannels.None);

        return notification.Channels & enabled;
    }

    private async Task<NotificationDeliveryResult> DeliverEmailAsync(
        QueuedNotification notification,
        CancellationToken cancellationToken)
    {
        if (email is null)
        {
            return Sent();
        }

        var user = await context.Query<User>()
            .SingleOrDefaultAsync(user => user.Id == notification.RecipientUserId, cancellationToken);

        if (user is null)
        {
            return Sent();
        }

        try
        {
            var outcome = await email.SendAsync(
                user.Email,
                notification.Kind,
                notification.Title.Trim(),
                notification.Body.Trim(),
                $"notification/{notification.Id}/email",
                cancellationToken);

            return outcome switch
            {
                NotificationEmailOutcome.Queued => Sent(),
                NotificationEmailOutcome.RetryableFailure => Transient("Email could not be queued."),
                _ => Permanent("Email was rejected before queuing.")
            };
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            logger.LogWarning("Email delivery failed with {ExceptionType}.", exception.GetType().Name);
            return Permanent("Email delivery failed.");
        }
    }

    private async Task<NotificationDeliveryResult> DeliverPushAsync(
        QueuedNotification notification,
        CancellationToken cancellationToken)
    {
        if (push is not { IsConfigured: true })
        {
            return Sent();
        }

        var devices = await context.Tracked<PushDevice>()
            .Where(device => device.UserId == notification.RecipientUserId && device.DisabledAt == null)
            .ToListAsync(cancellationToken);

        if (devices.Count == 0)
        {
            return Sent();
        }

        IReadOnlyDictionary<string, string> data;
        try
        {
            data = BuildData(notification);
        }
        catch (JsonException)
        {
            return Permanent("Notification routing data is invalid.");
        }

        PushSendResult result;
        try
        {
            result = await push.SendAsync(
                new PushMessage(
                    devices.Select(device => device.Token).ToArray(),
                    notification.Title,
                    notification.Body,
                    data),
                cancellationToken);
        }
        catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return Transient("Push provider timed out.");
        }
        catch (HttpRequestException)
        {
            return Transient("Push provider could not be reached.");
        }

        var outcomesByToken = result.Outcomes.ToDictionary(outcome => outcome.Token, StringComparer.Ordinal);
        var hasTransientFailure = false;
        var hasPermanentFailure = false;

        foreach (var device in devices)
        {
            if (!outcomesByToken.TryGetValue(device.Token, out var outcome))
            {
                hasPermanentFailure = true;
                continue;
            }

            switch (outcome.Status)
            {
                case PushDeliveryStatus.Delivered:
                    break;
                case PushDeliveryStatus.TokenDead:
                    device.DisabledAt = timeProvider.GetUtcNow();
                    break;
                case PushDeliveryStatus.TransientFailure:
                    hasTransientFailure = true;
                    break;
                case PushDeliveryStatus.PermanentFailure:
                    hasPermanentFailure = true;
                    break;
                default:
                    hasPermanentFailure = true;
                    break;
            }
        }

        if (hasPermanentFailure)
        {
            logger.LogWarning("The push provider permanently rejected one or more notification tokens.");
            return Permanent("The push provider rejected the notification.");
        }

        return hasTransientFailure
            ? Transient("The push provider returned a retryable failure.")
            : Sent();
    }

    private static IReadOnlyDictionary<string, string> BuildData(QueuedNotification notification)
    {
        var data = notification.DataJson is null
            ? new Dictionary<string, string>(StringComparer.Ordinal)
            : JsonSerializer.Deserialize<Dictionary<string, string>>(notification.DataJson)
                ?? throw new JsonException("Notification data must be an object.");

        if (data.Any(pair => string.IsNullOrWhiteSpace(pair.Key) || pair.Value is null))
        {
            throw new JsonException("Notification data contains an empty key or value.");
        }

        data["kind"] = notification.Kind;
        return data;
    }

    private static NotificationDeliveryResult Combine(
        IReadOnlyList<NotificationDeliveryResult> outcomes)
    {
        if (outcomes.Any(outcome => outcome.Disposition == NotificationDeliveryDisposition.TransientFailure))
        {
            return Transient("At least one notification channel returned a retryable failure.");
        }

        if (outcomes.Any(outcome => outcome.Disposition == NotificationDeliveryDisposition.PermanentFailure))
        {
            return Permanent("At least one notification channel permanently rejected the message.");
        }

        return Sent();
    }

    private static NotificationDeliveryResult Sent() =>
        new(NotificationDeliveryDisposition.Sent);

    private static NotificationDeliveryResult Transient(string error) =>
        new(NotificationDeliveryDisposition.TransientFailure, error);

    private static NotificationDeliveryResult Permanent(string error) =>
        new(NotificationDeliveryDisposition.PermanentFailure, error);
}
