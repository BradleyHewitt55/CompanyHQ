using CompanyHQ.Messages.Contracts.Enums;

namespace CompanyHQ.Messages.Contracts.Ports;

/// <summary>Optional, additive host rules for general or context-specific conversations.</summary>
public interface IMessageConversationPolicy
{
    /// <summary>Checks both accounts, application privileges, and any supplied context before starting or reusing a thread.</summary>
    Task<bool> CanStartAsync(Guid actingUserId, Guid participantId, string? contextKind, string? contextId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns conversation ids this caller may not access for this action. Message excludes them in SQL
    /// before counts, paging, unread totals, and attachment reads. Include revoked contexts and any
    /// application-wide restrictions; an empty set imposes no additional restriction.
    /// </summary>
    Task<IReadOnlySet<Guid>> RestrictedConversationIdsAsync(Guid actingUserId, ConversationAction action,
        CancellationToken cancellationToken = default);
}
