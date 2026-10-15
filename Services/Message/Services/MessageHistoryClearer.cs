using CompanyHQ.ServiceCatalog.Contracts;

using CompanyHQ.Messages.Contracts.Models;
using CompanyHQ.Persistence.Services;

namespace CompanyHQ.Messages.Services;

/// <summary>
/// Clears one participant's view of conversations; message rows remain immutable. The host calls it
/// after a report is filed against a message or an account.
/// </summary>
/// <remarks>
/// Takes <see cref="IPersistence"/> because every affected participant row is cleared in one
/// save; one repository update per row would be one transaction per row.
/// </remarks>
public sealed class MessageHistoryClearer(IServiceAvailability? availability, IPersistence context, TimeProvider timeProvider)
{
    /// <summary>Retains the original strict constructor for existing consumers.</summary>
    public MessageHistoryClearer(IPersistence context, TimeProvider timeProvider) : this(null, context, timeProvider) { }

    /// <summary>Clears the reporter's view of the conversation holding <paramref name="messageId"/>.</summary>
    public async Task ClearForMessageAsync(
        Guid reporterUserId,
        Guid messageId,
        CancellationToken cancellationToken = default)
    {
        await (availability?.RequireAsync("messages", false, cancellationToken) ?? Task.CompletedTask);
        var conversationIds = await context.Query<Message>()
            .Where(message => message.Id == messageId)
            .Select(message => message.ConversationId)
            .ToListAsync(cancellationToken);

        await ClearAsync(reporterUserId, conversationIds, cancellationToken);
    }

    /// <summary>Clears the reporter's view of every conversation shared with <paramref name="otherUserId"/>.</summary>
    public async Task ClearWithUserAsync(
        Guid reporterUserId,
        Guid otherUserId,
        CancellationToken cancellationToken = default)
    {
        await (availability?.RequireAsync("messages", false, cancellationToken) ?? Task.CompletedTask);
        var reporterConversations = context.Tracked<ConversationParticipant>()
            .Where(participant => participant.UserId == reporterUserId)
            .Select(participant => participant.ConversationId);

        var conversationIds = await context.Query<ConversationParticipant>()
            .Where(participant => participant.UserId == otherUserId
                && reporterConversations.Contains(participant.ConversationId))
            .Select(participant => participant.ConversationId)
            .ToListAsync(cancellationToken);

        await ClearAsync(reporterUserId, conversationIds, cancellationToken);
    }

    private async Task ClearAsync(
        Guid reporterUserId,
        List<Guid> conversationIds,
        CancellationToken cancellationToken)
    {
        if (conversationIds.Count == 0)
        {
            return;
        }

        var participants = await context.Tracked<ConversationParticipant>()
            .Where(participant => participant.UserId == reporterUserId
                && conversationIds.Contains(participant.ConversationId))
            .ToListAsync(cancellationToken);

        if (participants.Count == 0)
        {
            return;
        }

        var clearedAt = timeProvider.GetUtcNow();
        foreach (var participant in participants)
        {
            if (participant.ClearedAt is null || participant.ClearedAt < clearedAt)
            {
                participant.ClearedAt = clearedAt;
                participant.LastMessageAt = null;
                participant.LastReadAt = clearedAt;
            }
        }

        await context.SaveChangesAsync(cancellationToken);
    }
}
