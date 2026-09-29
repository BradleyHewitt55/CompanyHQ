using Microsoft.EntityFrameworkCore;

using CompanyHQ.Services.Messages.Contracts.Models;
using CompanyHQ.Services.Messages.Contracts.Ports;
using CompanyHQ.Services.Postgres.Services;
using CompanyHQ.Services.Users.Services;

namespace CompanyHQ.Services.Messages.Services;

/// <summary>
/// Whether a viewer may read a message attachment: a participant who can see its message row, or an
/// administrator once the conversation is closed — the same rule as the message text. The host hands
/// it to whichever media service is plugged in as that service's read policy.
/// </summary>
public sealed class MessageAttachmentAccess(
    IPostgresService<Message> messages,
    IPostgresService<ConversationParticipant> participants,
    IPostgresService<Conversation> conversations,
    IUserAuthorizationService authorization,
    IMessageRestrictions? restrictions = null)
{
    public async Task<bool> CanReadAsync(
        Guid mediaObjectId,
        Guid viewerUserId,
        CancellationToken cancellationToken = default)
    {
        if (mediaObjectId == Guid.Empty || viewerUserId == Guid.Empty)
        {
            return false;
        }

        // Operators review closed conversations in full and never see a live one, so an attachment
        // follows exactly that rule rather than a blanket admin bypass.
        if (await authorization.IsAdministratorAsync(viewerUserId, cancellationToken)
            && await (
                from message in messages.Query()
                join conversation in conversations.Query()
                    on message.ConversationId equals conversation.Id
                where message.MediaObjectId == mediaObjectId && conversation.ClosedAt != null
                select message.Id).AnyAsync(cancellationToken))
        {
            return true;
        }

        var restricted = restrictions is null
            ? (IReadOnlySet<Guid>)new HashSet<Guid>()
            : await restrictions.RestrictedMessageIdsAsync(cancellationToken);

        // Keep every client-side message restriction here as well as in the message read query: a
        // presigned URL would otherwise recover a hidden message through its attachment.
        var query =
            from message in messages.Query()
            join participant in participants.Query()
                on message.ConversationId equals participant.ConversationId
            where message.MediaObjectId == mediaObjectId
                && participant.UserId == viewerUserId
                && (message.SuppressedAt == null || message.SenderUserId == viewerUserId)
            select message.Id;

        if (restricted.Count > 0)
        {
            query = query.Where(messageId => !restricted.Contains(messageId));
        }

        return await query.AnyAsync(cancellationToken);
    }
}
