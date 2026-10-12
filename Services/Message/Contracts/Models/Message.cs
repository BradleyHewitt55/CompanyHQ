using CompanyHQ.Services.Persistence.Contracts.Models;

namespace CompanyHQ.Services.Messages.Contracts.Models;

/// <summary>One immutable text message in a conversation.</summary>
public sealed class Message : BaseModel
{
    public Guid ConversationId { get; set; }

    public Guid SenderUserId { get; set; }

    public required string Body { get; set; }

    /// <summary>Optional ready media owned by the sender and attached to this message.</summary>
    public Guid? MediaObjectId { get; set; }

    /// <summary>Client-generated id used to make retries idempotent for one sender.</summary>
    public Guid ClientMessageId { get; set; }

    /// <summary>Set when a block or moderation rule accepts the message but suppresses delivery.</summary>
    public DateTimeOffset? SuppressedAt { get; set; }
}
