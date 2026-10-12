using CompanyHQ.Services.Persistence.Contracts.Models;

namespace CompanyHQ.Services.Messages.Contracts.Models;

/// <summary>
/// The stable container for one two-party conversation.
/// </summary>
public sealed class Conversation : BaseModel
{
    /// <summary>
    /// The two participant ids in sorted order, joined with a colon. The database enforces that
    /// this pair plus the context key is unique so concurrent starts cannot fork a thread.
    /// </summary>
    public required string ParticipantKey { get; set; }

    /// <summary>Empty for a general thread; otherwise the normalized kind and opaque context id.</summary>
    public string ContextKey { get; set; } = string.Empty;

    public string? ContextKind { get; set; }

    public string? ContextId { get; set; }

    /// <summary>When set, both participants may read the history but neither may send again.</summary>
    public DateTimeOffset? ClosedAt { get; set; }

    /// <summary>The participant or conversation manager who closed the conversation.</summary>
    public Guid? ClosedByUserId { get; set; }
}
