
using CompanyHQ.Messages.Contracts.Models;
using CompanyHQ.Messages.Contracts.Ports;
using CompanyHQ.Persistence.Services;

namespace CompanyHQ.Messages.Services;

/// <summary>
/// Reads one message with a bounded window of its neighbours. Message uses it for spam-flag evidence;
/// the host hands it to whichever reporting service is plugged in.
/// </summary>
public sealed class MessageContextReader(IRepository<Message> messages)
{
    private const int ContextMessageCount = 2;

    /// <summary>Returns null when the message does not exist.</summary>
    public async Task<MessageContext?> ReadAsync(
        Guid messageId,
        CancellationToken cancellationToken = default)
    {
        var target = await messages.Query()
            .SingleOrDefaultAsync(message => message.Id == messageId, cancellationToken);

        if (target is null)
        {
            return null;
        }

        var before = await messages.Query()
            .Where(message => message.ConversationId == target.ConversationId
                && (message.CreatedAt < target.CreatedAt
                    || (message.CreatedAt == target.CreatedAt
                        && message.Id.CompareTo(target.Id) < 0)))
            .OrderByDescending(message => message.CreatedAt)
            .ThenByDescending(message => message.Id)
            .Take(ContextMessageCount)
            .ToListAsync(cancellationToken);

        var after = await messages.Query()
            .Where(message => message.ConversationId == target.ConversationId
                && (message.CreatedAt > target.CreatedAt
                    || (message.CreatedAt == target.CreatedAt
                        && message.Id.CompareTo(target.Id) > 0)))
            .OrderBy(message => message.CreatedAt)
            .ThenBy(message => message.Id)
            .Take(ContextMessageCount)
            .ToListAsync(cancellationToken);

        var captures = before
            .Append(target)
            .Concat(after)
            .OrderBy(message => message.CreatedAt)
            .ThenBy(message => message.Id)
            .Select(message => new MessageCapture(
                message.SenderUserId,
                message.Body,
                message.CreatedAt,
                message.Id == target.Id))
            .ToArray();

        return new MessageContext(target.SenderUserId, captures);
    }
}
