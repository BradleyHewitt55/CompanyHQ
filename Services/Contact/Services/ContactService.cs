using Microsoft.Extensions.Logging;

using CompanyHQ.Contact.Contracts.Dtos;
using CompanyHQ.Contact.Contracts.Enums;
using CompanyHQ.Contact.Contracts.Forms;
using CompanyHQ.Contact.Contracts.Ports;

namespace CompanyHQ.Contact.Services;

/// <inheritdoc cref="IContactService"/>
public sealed class ContactService(
    IContactMailer mailer,
    IContactAddressBook addressBook,
    TimeProvider clock,
    ILogger<ContactService> logger) : IContactService
{
    public async Task<ClientContactReceiptDto?> SubmitAsync(
        ContactRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var addresses = await addressBook.GetAsync(cancellationToken);
        if (addresses is null)
        {
            logger.LogError("Contact message could not be routed because no contact addresses are configured.");
            return null;
        }

        var recipient = request.Topic is ContactTopic.Support or ContactTopic.Privacy
            ? addresses.SupportEmail
            : addresses.InfoEmail;

        var reference = Guid.NewGuid();
        var values = new Dictionary<string, string?>
        {
            ["topic"] = request.Topic.ToString(),
            ["name"] = request.Name.Trim(),
            ["email"] = request.Email.Trim(),
            ["reference"] = reference.ToString(),
            ["message"] = request.Message.Trim()
        };

        // The reference is the idempotency key: a client retry of the same submission generates a new
        // one, which is correct: a visitor who sends twice meant to send twice.
        var error = await mailer.SendAsync(
            recipient,
            values,
            $"contact:{reference}",
            cancellationToken);

        if (error is not null)
        {
            logger.LogError("Contact message {Reference} was not queued: {Error}", reference, error);
            return null;
        }

        return new ClientContactReceiptDto(reference, request.Topic, clock.GetUtcNow());
    }
}
