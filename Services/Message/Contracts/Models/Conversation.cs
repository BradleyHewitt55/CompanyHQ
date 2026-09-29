using CompanyHQ.Services.Postgres.Contracts.Models;

namespace CompanyHQ.Services.Messages.Contracts.Models;

/// <summary>
/// The stable container for one two-party conversation.
/// </summary>
public sealed class Conversation : BaseModel
{
    /// <summary>
    /// The two participant ids in sorted order, joined with a colon. The database enforces that
    /// this key is unique so concurrent get-or-create requests cannot fork a thread.
    /// </summary>
    public required string ParticipantKey { get; set; }

    /// <summary>When set, both participants may read the history but neither may send again.</summary>
    public DateTimeOffset? ClosedAt { get; set; }

    /// <summary>The participant who closed the conversation.</summary>
    public Guid? ClosedByUserId { get; set; }
}
