using CompanyHQ.Services.Postgres.Contracts.Models;

namespace CompanyHQ.Services.Inbox.Contracts.Models;

/// <summary>One conversation a visitor without an account opened. The first message is the row itself.</summary>
public sealed class InboxConversation : BaseModel
{
    public required string Name { get; set; }

    public required string Email { get; set; }

    public required string Body { get; set; }

    /// <summary>Which site or app collected it, as the collecting service named it.</summary>
    public required string Source { get; set; }

    /// <summary>The page or screen the visitor was on, when the collecting service passed one.</summary>
    public string? Origin { get; set; }

    /// <summary>SHA-256 of the token the visitor holds. The token is the visitor's only credential.</summary>
    public required string VisitorTokenHash { get; set; }

    /// <summary>When the visitor or staff last wrote. Orders the staff list.</summary>
    public DateTimeOffset LastActivityAt { get; set; }

    public DateTimeOffset? ResolvedAt { get; set; }
}
