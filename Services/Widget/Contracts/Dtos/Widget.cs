namespace CompanyHQ.Widget.Contracts.Dtos;

/// <summary>What a widget can offer on this deployment right now, so a UI shows only what works.</summary>
public sealed record ClientWidgetDto(
    bool Messages,
    bool Issues,
    IReadOnlyList<string> Sources,
    IReadOnlyList<string> IssueCategories);

/// <summary>Handed to the visitor once, when a conversation is opened. The token is never shown again.</summary>
public sealed record ClientWidgetConversationAccessDto(Guid Id, string Token);

public sealed record ClientWidgetReplyDto(Guid Id, string Body, bool FromVisitor, DateTimeOffset CreatedAt);

/// <summary>The visitor's view of their own conversation.</summary>
public sealed record ClientWidgetConversationDto(
    Guid Id,
    string Name,
    string Message,
    DateTimeOffset CreatedAt,
    DateTimeOffset? ResolvedAt,
    IReadOnlyList<ClientWidgetReplyDto> Replies);
