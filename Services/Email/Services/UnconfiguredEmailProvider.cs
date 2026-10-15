using CompanyHQ.Email.Contracts.Ports;
using CompanyHQ.ServiceCatalog.Contracts;

namespace CompanyHQ.Email.Services;

internal sealed class UnconfiguredEmailProvider : IEmailProvider
{
    public Task<EmailSendResult> SendAsync(EmailMessage message, CancellationToken cancellationToken = default) =>
        throw new ServiceUnavailableException("email", ServiceStatus.EnabledNotConfigured);
}
