namespace CompanyHQ.Email.Contracts.Ports;

/// <summary>
/// Delivery port between the outbox and the transport. Email fills it itself with the host's Resend
/// client; it stays a port so the queue can be exercised without calling Resend.
/// </summary>
public interface IEmailProvider
{
    Task<EmailSendResult> SendAsync(EmailMessage message, CancellationToken cancellationToken = default);
}
