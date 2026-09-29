using CompanyHQ.Services.Company.Contracts.Ports;
using CompanyHQ.Services.Contact.Contracts.Ports;
using CompanyHQ.Services.Email.Contracts.Ports;

namespace Api.Integrations;

/// <summary>Fills Contact's address book with the company profile's published inboxes.</summary>
internal sealed class CompanyContactAddressBook(ICompanyProfileReader companyProfiles) : IContactAddressBook
{
    public async Task<ContactAddresses?> GetAsync(CancellationToken cancellationToken = default) =>
        await companyProfiles.GetAsync(cancellationToken) is { } company
            ? new ContactAddresses(company.SupportEmail, company.InfoEmail)
            : null;
}

/// <summary>Fills Contact's mailer with Email's controlled contact template.</summary>
internal sealed class EmailContactMailer(IEmailTemplateSender templates) : IContactMailer
{
    public async Task<string?> SendAsync(
        string recipient,
        IReadOnlyDictionary<string, string?> values,
        string idempotencyKey,
        CancellationToken cancellationToken = default)
    {
        var result = await templates.SendAsync(
            [recipient],
            EmailTemplateKeys.ContactMessage,
            values,
            idempotencyKey,
            cancellationToken);

        return result.IsSuccess ? null : result.Error ?? "Email was not queued.";
    }
}
