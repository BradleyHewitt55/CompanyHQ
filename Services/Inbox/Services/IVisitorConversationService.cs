namespace CompanyHQ.Inbox.Services;

/// <summary>What a collecting service hands over to open a conversation. All of it is anonymous public input.</summary>
public sealed record VisitorConversationStart(
    string Name,
    string Email,
    string Message,
    string Source,
    string? Origin = null);

/// <summary>Handed to the visitor once. The token is never stored or shown again.</summary>
public sealed record VisitorConversationAccess(Guid Id, string Token);

public sealed record VisitorConversationReply(Guid Id, string Body, bool FromVisitor, DateTimeOffset CreatedAt);

/// <summary>The visitor's view of their own conversation. It omits their address: they typed it.</summary>
public sealed record VisitorConversationThread(
    Guid Id,
    string Name,
    string Message,
    DateTimeOffset CreatedAt,
    DateTimeOffset? ResolvedAt,
    IReadOnlyList<VisitorConversationReply> Replies);

/// <summary>
/// The visitor half of the conversations Inbox stores, authorised by the conversation token alone.
/// It has no HTTP route of its own: whichever service faces the visitor reaches it through a bridge
/// in the host, and owns the rate limit.
/// </summary>
public interface IVisitorConversationService
{
    Task<VisitorConversationAccess> StartAsync(
        VisitorConversationStart start,
        CancellationToken cancellationToken = default);

    /// <summary>Null for an unknown id and for a wrong token alike.</summary>
    Task<VisitorConversationThread?> ReadAsync(
        Guid id,
        string token,
        CancellationToken cancellationToken = default);

    Task<Contracts.Enums.InboxError> SendAsync(
        Guid id,
        string token,
        string body,
        CancellationToken cancellationToken = default);

    /// <summary>Deletes conversations resolved longer ago than the retention window.</summary>
    Task<int> PurgeExpiredAsync(CancellationToken cancellationToken = default);
}
