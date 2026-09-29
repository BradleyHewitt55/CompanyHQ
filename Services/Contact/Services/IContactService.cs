using CompanyHQ.Services.Contact.Contracts.Dtos;
using CompanyHQ.Services.Contact.Contracts.Forms;

namespace CompanyHQ.Services.Contact.Services;

public interface IContactService
{
    /// <summary>Delivers one visitor message to the support or information inbox selected by topic.</summary>
    /// <returns>The receipt when delivery was accepted, otherwise <see langword="null"/>.</returns>
    Task<ClientContactReceiptDto?> SubmitAsync(ContactRequest request, CancellationToken cancellationToken = default);
}
