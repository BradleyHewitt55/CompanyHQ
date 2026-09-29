using CompanyHQ.Services.Email.Contracts.Models;
using CompanyHQ.Services.Email.Contracts.Ports;

namespace CompanyHQ.Services.Email.Services;

public interface IEmailService
{
    Task<EmailSendResult> SendAsync(EmailMessage message, CancellationToken cancellationToken = default);
}
