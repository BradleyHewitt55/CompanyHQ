using CompanyHQ.Services.Persistence.Contracts.Models;

namespace CompanyHQ.Services.Inbox.Contracts.Models;

/// <summary>A follow-up in a visitor conversation, written by the visitor or by staff.</summary>
public sealed class InboxReply : BaseModel
{
    public Guid InboxConversationId { get; set; }

    public required string Body { get; set; }

    public bool FromVisitor { get; set; }
}
