using CompanyHQ.Contact.Contracts.Enums;

namespace CompanyHQ.Contact.Contracts.Dtos;

/// <summary>
/// Acknowledgement that a contact message was accepted for delivery. The reference is what a visitor
/// quotes when they follow up, so it is generated here rather than taken from the mail provider —
/// the provider's id is an operational detail nobody outside the team can act on.
/// </summary>
public sealed record ClientContactReceiptDto(
    Guid Reference,
    ContactTopic Topic,
    DateTimeOffset ReceivedAt);
