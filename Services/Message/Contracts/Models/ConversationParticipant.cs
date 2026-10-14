using CompanyHQ.Persistence.Contracts.Models;

namespace CompanyHQ.Messages.Contracts.Models;

/// <summary>
/// Per-user state for a conversation. Ordering and read state belong here, not on the conversation.
/// </summary>
public sealed class ConversationParticipant : BaseModel
{
    public Guid ConversationId { get; set; }

    public Guid UserId { get; set; }

    public DateTimeOffset? LastMessageAt { get; set; }

    public DateTimeOffset? LastReadAt { get; set; }

    public DateTimeOffset? ClearedAt { get; set; }

    public DateTimeOffset? MutedAt { get; set; }
}
