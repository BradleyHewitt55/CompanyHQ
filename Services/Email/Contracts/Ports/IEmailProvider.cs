namespace CompanyHQ.Services.Email.Contracts.Ports;

/// <summary>
/// Provider-neutral delivery port. An adapter service (Resend) implements it by referencing this
/// Contracts assembly only. Required: without an adapter the host fails its build-time validation.
/// </summary>
public interface IEmailProvider
{
    Task<EmailSendResult> SendAsync(EmailMessage message, CancellationToken cancellationToken = default);
}
