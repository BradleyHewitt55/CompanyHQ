using CompanyHQ.Email.Contracts.Models;
using CompanyHQ.Email.Contracts.Ports;

namespace CompanyHQ.Email.Services;

public interface IEmailService
{
    Task<EmailSendResult> SendAsync(EmailMessage message, CancellationToken cancellationToken = default);
}
