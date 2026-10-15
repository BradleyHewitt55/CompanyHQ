using Microsoft.Extensions.Logging;
using CompanyHQ.Email.Contracts.Models;
using CompanyHQ.Email.Contracts.Ports;
using Resend;
using CompanyHQ.ServiceCatalog.Contracts;
using EmailMessage = CompanyHQ.Email.Contracts.Ports.EmailMessage;

namespace CompanyHQ.Email.Services;

/// <summary>
/// Sends one message through the host's <see cref="IResend"/> client. It makes a single attempt:
/// a retryable failure goes back to the outbox, which owns the backoff.
/// </summary>
internal sealed class ResendEmailProvider(
    IResend resend,
    EmailOptions options,
    EmailSendPacer pacer,
    ILogger<ResendEmailProvider> logger, IServiceAvailability? availability = null) : IEmailProvider
{
    public async Task<EmailSendResult> SendAsync(
        EmailMessage message,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(message);

        await pacer.WaitAsync(cancellationToken);
        if (availability is not null) await availability.RequireAsync("email", true, cancellationToken);

        try
        {
            var response = await resend.EmailSendAsync(
                message.IdempotencyKey,
                new global::Resend.EmailMessage
                {
                    From = options.From,
                    To = EmailAddressList.From(message.To),
                    Subject = message.Subject,
                    HtmlBody = message.Html,
                    TextBody = message.Text
                },
                cancellationToken);

            // The host may have turned ResendClientOptions.ThrowExceptions off.
            return response.Success
                ? EmailSendResult.Sent(response.Content.ToString())
                : Failed(response.Exception);
        }
        catch (ResendException exception)
        {
            return Failed(exception);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            logger.LogWarning("Resend request timed out.");
            return EmailSendResult.Failed(true, "Resend could not be reached.");
        }
    }

    private EmailSendResult Failed(ResendException? exception)
    {
        // The exception message can quote the request, so only its status and type are logged.
        var status = (int?)exception?.StatusCode;
        var retryable = exception is null || exception.IsTransient || status is null or >= 500;

        logger.LogWarning(
            "Resend failed an email request with status code {StatusCode} and error type {ErrorType}.",
            status,
            exception?.ErrorType);

        return EmailSendResult.Failed(
            retryable,
            retryable ? $"Resend was unavailable ({status})." : $"Resend rejected the email ({status}).");
    }
}
