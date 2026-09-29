using CompanyHQ.Services.Email.Contracts.Ports;
using CompanyHQ.Services.Notifications.Contracts.Ports;

namespace Api.Integrations;

/// <summary>Fills Notification's email channel with Email's controlled notification templates.</summary>
internal sealed class NotificationEmailSender(IEmailTemplateSender templates) : INotificationEmailSender
{
    public async Task<NotificationEmailOutcome> SendAsync(
        string recipient,
        string kind,
        string title,
        string body,
        string idempotencyKey,
        CancellationToken cancellationToken = default)
    {
        // A message notification never mails the message itself; every other kind carries its body.
        var isMessage = kind.Trim().ToLowerInvariant() is "message.new" or "message.received";
        var values = new Dictionary<string, string?> { ["title"] = title };
        if (!isMessage)
        {
            values["body"] = body;
        }

        var result = await templates.SendAsync(
            [recipient],
            isMessage ? EmailTemplateKeys.NotificationMessage : EmailTemplateKeys.NotificationGeneric,
            values,
            idempotencyKey,
            cancellationToken);

        if (result.IsSuccess)
        {
            return NotificationEmailOutcome.Queued;
        }

        return result.IsRetryable ? NotificationEmailOutcome.RetryableFailure : NotificationEmailOutcome.Rejected;
    }
}
