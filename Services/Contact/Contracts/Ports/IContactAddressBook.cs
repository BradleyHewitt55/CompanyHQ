namespace CompanyHQ.Services.Contact.Contracts.Ports;

/// <summary>
/// Where contact messages go. The host bridges it to whatever stores the company's published
/// addresses; Contact never references that service.
/// </summary>
public interface IContactAddressBook
{
    /// <summary>Returns null while no addresses are configured, which Contact treats as unroutable.</summary>
    Task<ContactAddresses?> GetAsync(CancellationToken cancellationToken = default);
}

/// <summary>The two inboxes a contact message can be routed to.</summary>
public sealed record ContactAddresses(string SupportEmail, string InfoEmail);
