using System.ComponentModel.DataAnnotations;

using CompanyHQ.Contact.Contracts.Enums;

namespace CompanyHQ.Contact.Contracts.Forms;

/// <summary>Body for <c>POST /api/contact</c>.</summary>
/// <remarks>
/// The sender's address is theirs to claim and is never trusted as an identity — it is only echoed
/// into the reply-to of the message staff receive.
/// </remarks>
public sealed record ContactRequest(
    [Required, StringLength(120, MinimumLength = 2)] string Name,
    [Required, EmailAddress, StringLength(254)] string Email,
    [Required, StringLength(4000, MinimumLength = 20)] string Message,
    [EnumDataType(typeof(ContactTopic))] ContactTopic Topic = ContactTopic.General);
