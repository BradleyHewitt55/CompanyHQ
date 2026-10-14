using System.Data;
using System.ComponentModel.DataAnnotations;
using CompanyHQ.Messages.Contracts.Enums;


using CompanyHQ.Messages.Contracts;
using CompanyHQ.Messages.Contracts.Dtos;
using CompanyHQ.Messages.Contracts.Forms;
using CompanyHQ.Messages.Contracts.Models;
using CompanyHQ.Messages.Contracts.Ports;
using CompanyHQ.Postgres.Contracts;
using CompanyHQ.Postgres.Contracts.Dtos;
using CompanyHQ.Postgres.Contracts.Forms;
using CompanyHQ.Persistence.Services;
using CompanyHQ.Users.Contracts.Models;
using CompanyHQ.Users.Services;

namespace CompanyHQ.Messages.Services;

/// <summary>Persistence and authorization boundary for participant-owned conversations.</summary>
/// <remarks>
/// Blocking, flagging, report restrictions, notifications, and attachments are Message's own optional
/// ports: Message works on its own, and each capability switches on when the host plugs an adapter
/// into it. With no attachment adapter, a send carrying an attachment is invalid.
/// </remarks>
internal sealed class MessageService(
    IPersistence context,
    IMessageBroadcaster broadcaster,
    TimeProvider timeProvider,
    MessageSpamDetector spamDetector,
    MessageContextReader contextReader,
    IUserAuthorizationService authorization,
    MessageAccessOptions access,
    MessageConversationRules rules,
    IMessageBlocks? blocks = null,
    IMessageAccountFlags? accountFlags = null,
    IMessageRestrictions? restrictions = null,
    IMessageNotifier? notifications = null,
    IMessageAttachments? attachments = null) : IMessageService
{
    private const int ThreadPageSize = 10;
    private const int PreviewLength = 120;
    private const string SpamKind = "message.spam";

    public async Task<PagedResult<ClientConversationDto>> ListConversationsAsync(
        Guid userId,
        PageRequest paging,
        CancellationToken cancellationToken = default)
    {
        var blocked = await BlockedForAsync(userId, cancellationToken);
        var restricted = await RestrictedMessagesAsync(userId, cancellationToken);
        var participants = await ConversationListQueryAsync(userId, cancellationToken);
        var total = await participants.CountAsync(cancellationToken);
        var page = participants
            .OrderByDescending(participant => participant.LastMessageAt)
            .ThenByDescending(participant => participant.ConversationId)
            .Skip(paging.Skip)
            .Take(paging.PageSize);
        var items = await ProjectConversationList(page, userId, blocked, restricted)
            .ToListAsync(cancellationToken);

        return new PagedResult<ClientConversationDto>(items, paging.Page, paging.PageSize, total);
    }

    public async Task<MessageOperationResult<ClientConversationDto>> StartConversationAsync(
        Guid userId,
        StartConversationRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (userId == Guid.Empty || request.ParticipantId == Guid.Empty || userId == request.ParticipantId
            || !Validator.TryValidateObject(request, new ValidationContext(request), [], validateAllProperties: true))
        {
            return MessageOperationResult<ClientConversationDto>.Invalid();
        }

        if (!await context.Query<User>()
                .AnyAsync(candidate => candidate.Id == request.ParticipantId, cancellationToken))
        {
            return MessageOperationResult<ClientConversationDto>.NotFound();
        }

        var kind = request.ContextKind?.Trim().ToLowerInvariant();
        var contextId = request.ContextId?.Trim();
        if (!await context.Tracked<User>().AnyAsync(candidate => candidate.Id == userId, cancellationToken))
            return MessageOperationResult<ClientConversationDto>.NotFound();
        if (!await rules.CanStartAsync(userId, request.ParticipantId, kind, contextId, cancellationToken))
            return MessageOperationResult<ClientConversationDto>.Forbidden();

        var conversation = await GetOrCreateConversationAsync(
            userId,
            request.ParticipantId,
            kind, contextId, cancellationToken);

        return await ProjectStartedConversationAsync(userId, conversation, cancellationToken);
    }

    public async Task<MessageOperationResult<MessagePageDto>> GetMessagesAsync(
        Guid userId,
        Guid conversationId,
        string? cursor = null,
        string? search = null,
        CancellationToken cancellationToken = default)
    {
        if (!await rules.AllowsAsync(userId, conversationId, ConversationAction.Read, cancellationToken))
            return MessageOperationResult<MessagePageDto>.NotFound();
        var participant = await context.Query<ConversationParticipant>()
            .SingleOrDefaultAsync(
                row => row.ConversationId == conversationId && row.UserId == userId,
                cancellationToken);

        if (participant is null)
        {
            return MessageOperationResult<MessagePageDto>.NotFound();
        }

        var closedAt = await context.Query<Conversation>()
            .Where(conversation => conversation.Id == conversationId)
            .Select(conversation => conversation.ClosedAt)
            .SingleOrDefaultAsync(cancellationToken);

        if (!MessageCursor.TryDecode(cursor, out var decodedCursor))
        {
            return MessageOperationResult<MessagePageDto>.Invalid();
        }

        var blocked = await BlockedForAsync(userId, cancellationToken);
        var restricted = await RestrictedMessagesAsync(userId, cancellationToken);
        var query = VisibleMessages(userId, blocked, restricted, participant.ClearedAt)
            .Where(message => message.ConversationId == conversationId);

        if (!string.IsNullOrWhiteSpace(search))
        {
            query = query.Where(message => QueryFunctions.LikeIgnoreCase(message.Body, $"%{search.Trim()}%"));
        }

        if (decodedCursor is { } key)
        {
            query = query.Where(message =>
                message.CreatedAt < key.CreatedAt
                || (message.CreatedAt == key.CreatedAt && message.Id.CompareTo(key.Id) < 0));
        }

        var rows = await query
            .OrderByDescending(message => message.CreatedAt)
            .ThenByDescending(message => message.Id)
            .Take(ThreadPageSize + 1)
            .Select(message => new ClientMessageDto(
                message.Id,
                message.ConversationId,
                message.SenderUserId,
                message.Body,
                message.MediaObjectId,
                message.CreatedAt))
            .ToListAsync(cancellationToken);

        var hasNext = rows.Count > ThreadPageSize;
        if (hasNext)
        {
            rows.RemoveAt(rows.Count - 1);
        }

        var nextCursor = hasNext && rows.Count > 0
            ? MessageCursor.Encode(rows[^1].CreatedAt, rows[^1].Id)
            : null;

        return MessageOperationResult<MessagePageDto>.Success(
            new MessagePageDto(rows, nextCursor, closedAt is not null, closedAt));
    }

    public async Task<MessageOperationResult<SendMessageResult>> SendMessageAsync(
        Guid userId,
        Guid conversationId,
        SendMessageRequest request,
        CancellationToken cancellationToken = default)
    {
        if (!await rules.AllowsAsync(userId, conversationId, ConversationAction.Send, cancellationToken)
            || !await rules.AllowsAsync(userId, conversationId, ConversationAction.Read, cancellationToken))
            return MessageOperationResult<SendMessageResult>.NotFound();
        var participants = await context.Tracked<ConversationParticipant>()
            .Where(row => row.ConversationId == conversationId)
            .ToListAsync(cancellationToken);

        // The caller's own row is resolved first. Picking the "other" side before confirming the
        // caller is a participant matches both rows for an outsider, and a throw there would answer
        // "this conversation exists" with a 500 where every other route answers 404.
        var sender = participants.SingleOrDefault(row => row.UserId == userId);

        if (sender is null)
        {
            return MessageOperationResult<SendMessageResult>.NotFound();
        }

        var recipient = participants.SingleOrDefault(row => row.UserId != userId);

        if (recipient is null)
        {
            return MessageOperationResult<SendMessageResult>.NotFound();
        }

        if (access.RequireVerifiedEmail && !await context.Tracked<User>().AnyAsync(
                account => account.Id == userId && account.EmailVerified, cancellationToken))
            return MessageOperationResult<SendMessageResult>.Forbidden();

        if (!await rules.AllowsAsync(recipient.UserId, conversationId, ConversationAction.Read, cancellationToken))
            return MessageOperationResult<SendMessageResult>.Forbidden();

        var existing = await context.Query<Message>()
            .SingleOrDefaultAsync(
                message => message.SenderUserId == userId
                    && message.ClientMessageId == request.ClientMessageId,
                cancellationToken);

        if (existing is not null)
        {
            if (existing.ConversationId != conversationId)
            {
                return MessageOperationResult<SendMessageResult>.Conflict();
            }

            return MessageOperationResult<SendMessageResult>.Success(
                new SendMessageResult(ToClientMessage(existing), true));
        }

        var isClosed = await context.Query<Conversation>()
            .Where(conversation => conversation.Id == conversationId)
            .Select(conversation => conversation.ClosedAt != null)
            .SingleOrDefaultAsync(cancellationToken);
        if (isClosed)
        {
            return MessageOperationResult<SendMessageResult>.Conflict();
        }

        if (request.MediaObjectId is { } mediaObjectId
            && (attachments is null
                || !await attachments.IsReadyAsync(mediaObjectId, userId, cancellationToken)))
        {
            return MessageOperationResult<SendMessageResult>.Invalid();
        }

        var now = timeProvider.GetUtcNow();
        var hasPreviousMessage = await context.Query<Message>()
            .AnyAsync(
                message => message.ConversationId == conversationId && message.SenderUserId == userId,
                cancellationToken);
        var spamDecision = await spamDetector.RecordAsync(
            userId,
            conversationId,
            !hasPreviousMessage,
            cancellationToken);
        var senderFlagged = accountFlags is not null
            && await accountFlags.IsFlaggedAsync(userId, SpamKind, cancellationToken);
        var recipientFlagged = accountFlags is not null
            && await accountFlags.IsFlaggedAsync(recipient.UserId, SpamKind, cancellationToken);
        var recipientIsOperator = await authorization.HasPermissionAsync(
            recipient.UserId, AccessPermissions.ManageConversations, cancellationToken);
        var spamSuppressed = !recipientIsOperator
            && !recipientFlagged
            && (!spamDecision.IsAvailable || senderFlagged || spamDecision.ShouldFlag);
        var blocked = blocks is not null
            && await blocks.IsBlockedBetweenAsync(userId, recipient.UserId, cancellationToken);
        var suppressed = blocked || spamSuppressed;
        var message = new Message
        {
            ConversationId = conversationId,
            SenderUserId = userId,
            Body = request.Body,
            MediaObjectId = request.MediaObjectId,
            ClientMessageId = request.ClientMessageId,
            SuppressedAt = suppressed ? now : null
        };

        context.Add(message);
        sender.LastMessageAt = now;
        if (!suppressed)
        {
            recipient.LastMessageAt = now;
        }

        await context.SaveChangesAsync(cancellationToken);

        if (spamDecision.ShouldFlag && !senderFlagged && accountFlags is not null)
        {
            var evidence = await contextReader.ReadAsync(message.Id, cancellationToken);
            await accountFlags.RaiseAsync(
                userId,
                SpamKind,
                spamDetector.BuildEvidence(spamDecision, evidence?.Captures ?? []),
                cancellationToken);
            await NotifyOperatorAsync(userId, spamDecision, cancellationToken);
        }

        if (!suppressed && recipient.MutedAt is null && notifications is not null)
        {
            await notifications.EnqueueAsync(
                new MessageNotification(
                    recipient.UserId,
                    "message.new",
                    "New message",
                    "You have a new message.",
                    new Dictionary<string, string>
                    {
                        ["conversationId"] = conversationId.ToString("D")
                    }),
                cancellationToken);
            await context.SaveChangesAsync(cancellationToken);
        }

        var dto = ToClientMessage(message);
        if (!suppressed)
        {
            await broadcaster.BroadcastMessageAsync(dto, userId, recipient.UserId, cancellationToken);
        }

        return MessageOperationResult<SendMessageResult>.Success(new SendMessageResult(dto, false));
    }

    public async Task<MessageOperationResult<DateTimeOffset>> MarkReadAsync(
        Guid userId,
        Guid conversationId,
        CancellationToken cancellationToken = default)
    {
        if (!await rules.AllowsAsync(userId, conversationId, ConversationAction.Read, cancellationToken))
            return MessageOperationResult<DateTimeOffset>.NotFound();
        var participant = await context.Tracked<ConversationParticipant>()
            .SingleOrDefaultAsync(
                row => row.ConversationId == conversationId && row.UserId == userId,
                cancellationToken);

        if (participant is null)
        {
            return MessageOperationResult<DateTimeOffset>.NotFound();
        }

        var readAt = timeProvider.GetUtcNow();
        participant.LastReadAt = readAt;
        await context.SaveChangesAsync(cancellationToken);

        var otherParticipantId = await context.Query<ConversationParticipant>()
            .Where(row => row.ConversationId == conversationId && row.UserId != userId)
            .Select(row => row.UserId)
            .SingleAsync(cancellationToken);

        if (await rules.AllowsAsync(otherParticipantId, conversationId, ConversationAction.Read, cancellationToken))
            await broadcaster.BroadcastReadAsync(
            conversationId,
            readAt,
            userId,
            otherParticipantId,
            cancellationToken);

        return MessageOperationResult<DateTimeOffset>.Success(readAt);
    }

    public async Task<MessageOperationResult<bool>> SetMuteAsync(
        Guid userId,
        Guid conversationId,
        bool muted,
        CancellationToken cancellationToken = default)
    {
        if (!await rules.AllowsAsync(userId, conversationId, ConversationAction.Read, cancellationToken))
            return MessageOperationResult<bool>.NotFound();
        var participant = await context.Tracked<ConversationParticipant>()
            .SingleOrDefaultAsync(
                row => row.ConversationId == conversationId && row.UserId == userId,
                cancellationToken);

        if (participant is null)
        {
            return MessageOperationResult<bool>.NotFound();
        }

        participant.MutedAt = muted ? timeProvider.GetUtcNow() : null;
        await context.SaveChangesAsync(cancellationToken);
        return MessageOperationResult<bool>.Success(muted);
    }

    public async Task<MessageOperationResult<bool>> ClearAsync(
        Guid userId,
        Guid conversationId,
        CancellationToken cancellationToken = default)
    {
        if (!await rules.AllowsAsync(userId, conversationId, ConversationAction.Read, cancellationToken))
            return MessageOperationResult<bool>.NotFound();
        var participant = await context.Tracked<ConversationParticipant>()
            .SingleOrDefaultAsync(
                row => row.ConversationId == conversationId && row.UserId == userId,
                cancellationToken);

        if (participant is null)
        {
            return MessageOperationResult<bool>.NotFound();
        }

        var clearedAt = timeProvider.GetUtcNow();
        participant.ClearedAt = clearedAt;
        participant.LastMessageAt = null;
        participant.LastReadAt = clearedAt;
        await context.SaveChangesAsync(cancellationToken);
        return MessageOperationResult<bool>.Success(true);
    }

    public async Task<MessageOperationResult<bool>> CloseAsync(
        Guid userId,
        Guid conversationId,
        CancellationToken cancellationToken = default)
    {
        if (!await rules.AllowsAsync(userId, conversationId, ConversationAction.Close, cancellationToken))
            return MessageOperationResult<bool>.NotFound();
        var conversation = await context.Tracked<Conversation>()
            .SingleOrDefaultAsync(candidate => candidate.Id == conversationId, cancellationToken);
        var participants = await context.Tracked<ConversationParticipant>()
            .Where(row => row.ConversationId == conversationId)
            .ToListAsync(cancellationToken);
        var isManager = await authorization.HasPermissionAsync(
            userId, AccessPermissions.ManageConversations, cancellationToken);

        // A manager may act on a conversation they are not in; anyone else still gets the 404.
        if (conversation is null || (!isManager && participants.All(row => row.UserId != userId)))
        {
            return MessageOperationResult<bool>.NotFound();
        }

        if (!MessageAccessOptions.Allows(access.Close, isManager))
        {
            return MessageOperationResult<bool>.Forbidden();
        }

        if (conversation.ClosedAt is not null)
        {
            return MessageOperationResult<bool>.Success(true);
        }

        var now = timeProvider.GetUtcNow();
        conversation.ClosedAt = now;
        conversation.ClosedByUserId = userId;
        await context.SaveChangesAsync(cancellationToken);

        var administratorIds = await authorization.ListUserIdsAsync(
            AccessPermissions.ManageConversations, cancellationToken);

        var notificationsToQueue = participants
            .Where(row => row.UserId != userId && !administratorIds.Contains(row.UserId))
            .Select(row => new MessageNotification(
                row.UserId,
                "message.closed",
                "Conversation closed",
                "Your conversation has been closed.",
                new Dictionary<string, string>
                {
                    ["conversationId"] = conversationId.ToString("D")
                }))
            .ToList();

        notificationsToQueue.AddRange(
            administratorIds
                .Where(administratorId => administratorId != userId)
                .Select(administratorId => new MessageNotification(
                    administratorId,
                    "message.closed",
                    "Conversation closed",
                    "A conversation was closed and is ready for history review.",
                    new Dictionary<string, string>
                    {
                        ["conversationId"] = conversationId.ToString("D")
                    })));

        if (notifications is not null)
        {
            await notifications.EnqueueManyAsync(notificationsToQueue, cancellationToken);
            await context.SaveChangesAsync(cancellationToken);
        }

        return MessageOperationResult<bool>.Success(true);
    }

    public async Task<MessageOperationResult<bool>> DeleteAsync(
        Guid userId,
        Guid conversationId,
        CancellationToken cancellationToken = default)
    {
        if (!await rules.AllowsAsync(userId, conversationId, ConversationAction.Delete, cancellationToken))
            return MessageOperationResult<bool>.NotFound();
        var conversation = await context.Tracked<Conversation>()
            .SingleOrDefaultAsync(candidate => candidate.Id == conversationId, cancellationToken);
        var isManager = await authorization.HasPermissionAsync(
            userId, AccessPermissions.ManageConversations, cancellationToken);

        if (conversation is null
            || (!isManager && !await context.Tracked<ConversationParticipant>().AnyAsync(
                row => row.ConversationId == conversationId && row.UserId == userId,
                cancellationToken)))
        {
            return MessageOperationResult<bool>.NotFound();
        }

        if (!MessageAccessOptions.Allows(access.Delete, isManager))
        {
            return MessageOperationResult<bool>.Forbidden();
        }

        var attached = await context.Tracked<Message>()
            .Where(message => message.ConversationId == conversationId && message.MediaObjectId != null)
            .Select(message => message.MediaObjectId!.Value)
            .Distinct()
            .ToListAsync(cancellationToken);

        // Participant and message rows go with it through their cascading foreign keys.
        context.Remove(conversation);
        await context.SaveChangesAsync(cancellationToken);

        // After the rows are gone, so a failed save never strands a message pointing at a deleted file.
        if (attachments is not null && attached.Count > 0)
        {
            await attachments.DeleteAsync(attached, userId, cancellationToken);
        }

        return MessageOperationResult<bool>.Success(true);
    }

    public async Task<PagedResult<AdminConversationDto>> ListClosedConversationsAsync(
        Guid administratorId,
        PageRequest paging,
        string? search = null,
        CancellationToken cancellationToken = default)
    {
        if (!await authorization.HasPermissionAsync(administratorId, AccessPermissions.ManageConversations, cancellationToken))
        {
            return PagedResult<AdminConversationDto>.Empty(paging);
        }

        var denied = await rules.RestrictedAsync(administratorId, ConversationAction.Read, cancellationToken);
        var query = context.Query<Conversation>()
            .Where(conversation => conversation.ClosedAt != null && !denied.Contains(conversation.Id));
        if (!string.IsNullOrWhiteSpace(search))
        {
            // Matched in SQL against the two things the admin list actually shows: who was talking
            // and what was said. Paging a filtered-in-memory page would drop rows off later pages.
            var pattern = $"%{search.Trim()}%";
            query = query.Where(conversation =>
                context.Tracked<Message>().Any(message =>
                    message.ConversationId == conversation.Id
                    && QueryFunctions.LikeIgnoreCase(message.Body, pattern))
                || context.Tracked<ConversationParticipant>().Any(participant =>
                    participant.ConversationId == conversation.Id
                    && context.Tracked<User>().Any(user =>
                        user.Id == participant.UserId
                        && QueryFunctions.LikeIgnoreCase((user.FirstName ?? "") + " " + (user.LastName ?? ""), pattern))));
        }

        var total = await query.CountAsync(cancellationToken);
        var page = await query
            .OrderByDescending(conversation => conversation.ClosedAt)
            .ThenByDescending(conversation => conversation.Id)
            .Skip(paging.Skip)
            .Take(paging.PageSize)
            .Select(conversation => new
            {
                conversation.Id,
                conversation.ClosedAt,
                conversation.ClosedByUserId,
                conversation.ContextKind,
                conversation.ContextId
            })
            .ToListAsync(cancellationToken);

        var conversationIds = page.Select(row => row.Id).ToArray();
        if (conversationIds.Length == 0)
        {
            return new PagedResult<AdminConversationDto>([], paging.Page, paging.PageSize, total);
        }

        var participantRows = await context.Query<ConversationParticipant>()
            .Where(row => conversationIds.Contains(row.ConversationId))
            .Select(row => new { row.ConversationId, row.UserId })
            .ToListAsync(cancellationToken);
        var userIds = participantRows.Select(row => row.UserId).Distinct().ToArray();
        var names = await context.Query<User>()
            .Where(user => userIds.Contains(user.Id))
            .ToDictionaryAsync(user => user.Id, user => new { user.FirstName, user.LastName }, cancellationToken);
        var messageRows = await context.Query<Message>()
            .Where(message => conversationIds.Contains(message.ConversationId))
            .Select(message => new
            {
                message.ConversationId,
                message.CreatedAt,
                message.Body
            })
            .ToListAsync(cancellationToken);

        var messagesByConversation = messageRows
            .GroupBy(row => row.ConversationId)
            .ToDictionary(
                group => group.Key,
                group => group.OrderByDescending(row => row.CreatedAt).First());
        var countsByConversation = messageRows
            .GroupBy(row => row.ConversationId)
            .ToDictionary(group => group.Key, group => group.Count());
        var participantsByConversation = participantRows
            .GroupBy(row => row.ConversationId)
            .ToDictionary(
                group => group.Key,
                group => (IReadOnlyList<AdminConversationParticipantDto>)group
                    .Select(row => new AdminConversationParticipantDto(
                        row.UserId,
                        names.GetValueOrDefault(row.UserId)?.FirstName,
                        names.GetValueOrDefault(row.UserId)?.LastName))
                    .ToList());

        var items = page.Select(row =>
        {
            messagesByConversation.TryGetValue(row.Id, out var lastMessage);
            return new AdminConversationDto(
                row.Id,
                participantsByConversation.GetValueOrDefault(row.Id, []),
                lastMessage?.CreatedAt,
                lastMessage?.Body,
                row.ClosedAt!.Value,
                row.ClosedByUserId,
                countsByConversation.GetValueOrDefault(row.Id), row.ContextKind, row.ContextId);
        }).ToList();

        return new PagedResult<AdminConversationDto>(items, paging.Page, paging.PageSize, total);
    }

    public async Task<MessageOperationResult<MessagePageDto>> GetClosedMessagesAsync(
        Guid administratorId,
        Guid conversationId,
        string? cursor = null,
        string? search = null,
        CancellationToken cancellationToken = default)
    {
        if (!await rules.AllowsAsync(administratorId, conversationId, ConversationAction.Read, cancellationToken)
            || !await authorization.HasPermissionAsync(administratorId, AccessPermissions.ManageConversations, cancellationToken)
            || !await context.Query<Conversation>()
                .AnyAsync(
                    conversation => conversation.Id == conversationId && conversation.ClosedAt != null,
                    cancellationToken))
        {
            return MessageOperationResult<MessagePageDto>.NotFound();
        }

        var closedAt = await context.Query<Conversation>()
            .Where(conversation => conversation.Id == conversationId)
            .Select(conversation => conversation.ClosedAt)
            .SingleOrDefaultAsync(cancellationToken);
        if (closedAt is null)
        {
            return MessageOperationResult<MessagePageDto>.NotFound();
        }

        if (!MessageCursor.TryDecode(cursor, out var decodedCursor))
        {
            return MessageOperationResult<MessagePageDto>.Invalid();
        }

        var query = context.Query<Message>()
            .Where(message => message.ConversationId == conversationId);
        if (!string.IsNullOrWhiteSpace(search))
        {
            query = query.Where(message => QueryFunctions.LikeIgnoreCase(message.Body, $"%{search.Trim()}%"));
        }

        if (decodedCursor is { } key)
        {
            query = query.Where(message =>
                message.CreatedAt < key.CreatedAt
                || (message.CreatedAt == key.CreatedAt && message.Id.CompareTo(key.Id) < 0));
        }

        var rows = await query
            .OrderByDescending(message => message.CreatedAt)
            .ThenByDescending(message => message.Id)
            .Take(ThreadPageSize + 1)
            .Select(message => new ClientMessageDto(
                message.Id,
                message.ConversationId,
                message.SenderUserId,
                message.Body,
                message.MediaObjectId,
                message.CreatedAt))
            .ToListAsync(cancellationToken);
        var hasNext = rows.Count > ThreadPageSize;
        if (hasNext)
        {
            rows.RemoveAt(rows.Count - 1);
        }

        var nextCursor = hasNext && rows.Count > 0
            ? MessageCursor.Encode(rows[^1].CreatedAt, rows[^1].Id)
            : null;
        return MessageOperationResult<MessagePageDto>.Success(
            new MessagePageDto(rows, nextCursor, closedAt is not null, closedAt));
    }

    public async Task<MessageOperationResult<ClientConversationDto>> StartOperatorConversationAsync(
        Guid administratorId,
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        if (!await authorization.HasPermissionAsync(administratorId, AccessPermissions.ManageConversations, cancellationToken))
        {
            return MessageOperationResult<ClientConversationDto>.Invalid();
        }

        if (administratorId == userId)
        {
            return MessageOperationResult<ClientConversationDto>.Invalid();
        }

        if (!await context.Query<User>()
                .AnyAsync(candidate => candidate.Id == userId, cancellationToken))
        {
            return MessageOperationResult<ClientConversationDto>.NotFound();
        }

        return await StartConversationAsync(administratorId, new StartConversationRequest(userId), cancellationToken);
    }

    private async Task<MessageOperationResult<ClientConversationDto>> ProjectStartedConversationAsync(
        Guid userId,
        Conversation conversation,
        CancellationToken cancellationToken)
    {
        var blocked = await BlockedForAsync(userId, cancellationToken);
        var restricted = await RestrictedMessagesAsync(userId, cancellationToken);
        var result = await ProjectConversationList(
                (await ConversationListQueryAsync(userId, cancellationToken)).Where(row => row.ConversationId == conversation.Id),
                userId,
                blocked,
                restricted)
            .FirstOrDefaultAsync(cancellationToken);

        return result is null
            ? MessageOperationResult<ClientConversationDto>.NotFound()
            : MessageOperationResult<ClientConversationDto>.Success(result);
    }

    public async Task<int> GetUnreadCountAsync(
        Guid userId,
        CancellationToken cancellationToken = default) =>
        await GetUnreadCountCoreAsync(userId, cancellationToken);

    private async Task<int> GetUnreadCountCoreAsync(
        Guid userId,
        CancellationToken cancellationToken)
    {
        var blocked = await BlockedForAsync(userId, cancellationToken);
        var restricted = await RestrictedMessagesAsync(userId, cancellationToken);
        var visibleMessages = VisibleMessages(userId, blocked, restricted);

        var participants = await ConversationListQueryAsync(userId, cancellationToken);

        // A person has one participant row per conversation (a unique index says so), so a message
        // can match at most one of them: counting the messages that have such a row is the same
        // count as pairing each row with its messages.
        return await visibleMessages
            .Where(message => message.SenderUserId != userId
                && participants.Any(participant =>
                    participant.ConversationId == message.ConversationId
                    && (participant.ClearedAt == null || message.CreatedAt > participant.ClearedAt)
                    && (participant.LastReadAt == null || message.CreatedAt > participant.LastReadAt)))
            .CountAsync(cancellationToken);
    }

    private async Task<IQuery<ConversationParticipant>> ConversationListQueryAsync(Guid userId, CancellationToken ct)
    {
        var denied = await rules.RestrictedAsync(userId, ConversationAction.Read, ct);
        return context.Query<ConversationParticipant>()
            .Where(participant => participant.UserId == userId && !denied.Contains(participant.ConversationId));
    }

    private IQuery<ClientConversationDto> ProjectConversationList(
        IQuery<ConversationParticipant> participants,
        Guid userId,
        IReadOnlySet<Guid> blocked,
        IReadOnlySet<Guid> restricted)
    {
        var visibleMessages = VisibleMessages(userId, blocked, restricted);

        return
            from participant in participants
            let otherUserId = context.Tracked<ConversationParticipant>()
                .Where(other => other.ConversationId == participant.ConversationId && other.UserId != userId)
                .Select(other => other.UserId)
                .FirstOrDefault()
            let lastMessage = visibleMessages
                .Where(message => message.ConversationId == participant.ConversationId
                    && (participant.ClearedAt == null || message.CreatedAt > participant.ClearedAt))
                .OrderByDescending(message => message.CreatedAt)
                .ThenByDescending(message => message.Id)
                .Select(message => new { message.CreatedAt, message.Body })
                .FirstOrDefault()
            select new ClientConversationDto(
                participant.ConversationId,
                otherUserId,
                context.Tracked<User>()
                    .Where(user => user.Id == otherUserId)
                    .Select(user => user.FirstName)
                    .FirstOrDefault(),
                context.Tracked<User>()
                    .Where(user => user.Id == otherUserId)
                    .Select(user => user.LastName)
                    .FirstOrDefault(),
                participant.LastMessageAt,
                lastMessage == null
                    ? null
                    : lastMessage.Body.Length > PreviewLength
                        ? lastMessage.Body.Substring(0, PreviewLength)
                        : lastMessage.Body,
                visibleMessages
                    .Where(message =>
                        message.ConversationId == participant.ConversationId
                        && (participant.ClearedAt == null || message.CreatedAt > participant.ClearedAt)
                        && message.SenderUserId != userId
                        && (participant.LastReadAt == null || message.CreatedAt > participant.LastReadAt))
                    .Count(),
                context.Tracked<Conversation>()
                    .Where(conversation => conversation.Id == participant.ConversationId)
                    .Select(conversation => conversation.ClosedAt != null)
                    .FirstOrDefault(),
                context.Tracked<Conversation>()
                    .Where(conversation => conversation.Id == participant.ConversationId)
                    .Select(conversation => conversation.ClosedAt)
                    .FirstOrDefault(),
                context.Tracked<Conversation>().Where(conversation => conversation.Id == participant.ConversationId)
                    .Select(conversation => conversation.ContextKind).FirstOrDefault(),
                context.Tracked<Conversation>().Where(conversation => conversation.Id == participant.ConversationId)
                    .Select(conversation => conversation.ContextId).FirstOrDefault());
    }

    /// <summary>
    /// The single visibility query used by every participant read path. A sender can see their own
    /// suppressed rows; every other suppressed or blocked sender is removed in SQL before paging.
    /// </summary>
    private IQuery<Message> VisibleMessages(
        Guid userId,
        IReadOnlySet<Guid> blocked,
        IReadOnlySet<Guid> restricted,
        DateTimeOffset? clearedAt = null)
    {
        var query = context.Query<Message>()
            .Where(message => message.SenderUserId == userId || message.SuppressedAt == null);

        if (restricted.Count > 0)
        {
            query = query.Where(message => !restricted.Contains(message.Id));
        }

        if (blocked.Count > 0)
        {
            query = query.Where(message => message.SenderUserId == userId || !blocked.Contains(message.SenderUserId));
        }

        return clearedAt is null
            ? query
            : query.Where(message => message.CreatedAt > clearedAt.Value);
    }

    private async Task<IReadOnlySet<Guid>> BlockedForAsync(Guid userId, CancellationToken cancellationToken) =>
        blocks is null ? new HashSet<Guid>() : await blocks.BlockedForAsync(userId, cancellationToken);

    private async Task<IReadOnlySet<Guid>> RestrictedMessagesAsync(
        Guid userId,
        CancellationToken cancellationToken)
    {
        if (restrictions is null
            || await authorization.HasPermissionAsync(userId, AccessPermissions.ManageConversations, cancellationToken))
        {
            return new HashSet<Guid>();
        }

        return await restrictions.RestrictedMessageIdsAsync(cancellationToken);
    }

    private async Task NotifyOperatorAsync(
        Guid flaggedUserId,
        MessageSpamDecision decision,
        CancellationToken cancellationToken)
    {
        var operatorIds = await authorization.ListUserIdsAsync(
            AccessPermissions.ManageConversations, cancellationToken);

        if (operatorIds.Count == 0 || notifications is null)
        {
            return;
        }

        var adminId = operatorIds[0];

        await notifications.EnqueueAsync(
            new MessageNotification(
                adminId,
                "message.spam",
                "Message account needs review",
                "An account exceeded the automated messaging thresholds.",
                new Dictionary<string, string>
                {
                    ["userId"] = flaggedUserId.ToString("D"),
                    ["volume"] = decision.VolumeCount.ToString(),
                    ["spread"] = decision.SpreadCount.ToString(),
                    ["windowMinutes"] = decision.Window.TotalMinutes.ToString("0")
                }),
            cancellationToken);

        // Notification enqueue intentionally does not save. This send owns the unit of work after
        // the flag has been written, so the queued row lands with the rest of this decision.
        await context.SaveChangesAsync(cancellationToken);
    }

    private async Task<Conversation> GetOrCreateConversationAsync(
        Guid userId,
        Guid otherUserId,
        string? contextKind,
        string? contextId,
        CancellationToken cancellationToken)
    {
        var contextKey = contextKind is null ? string.Empty : $"{contextKind}:{contextId}";
        var participantKey = BuildParticipantKey(userId, otherUserId);
        var existing = await context.Tracked<Conversation>()
            .SingleOrDefaultAsync(conversation => conversation.ParticipantKey == participantKey && conversation.ContextKey == contextKey, cancellationToken);

        if (existing is not null)
        {
            return existing;
        }

        return await CreateConversationAsync(userId, otherUserId, participantKey, contextKey, contextKind, contextId, cancellationToken);
    }

    private async Task<Conversation> CreateConversationAsync(
        Guid userId,
        Guid otherUserId,
        string participantKey,
        string contextKey,
        string? contextKind,
        string? contextId,
        CancellationToken cancellationToken)
    {
        try
        {
            // One transaction, which the provider may retry as a whole. The lookup inside it is
            // repeated on a retry, which is what makes it safe.
            return await context.InTransactionAsync(async _ =>
            {
                var existing = await context.Tracked<Conversation>()
                    .SingleOrDefaultAsync(conversation => conversation.ParticipantKey == participantKey && conversation.ContextKey == contextKey, cancellationToken);

                if (existing is not null)
                {
                    return existing;
                }

                var conversation = new Conversation
                {
                    ParticipantKey = participantKey,
                    ContextKey = contextKey,
                    ContextKind = contextKind,
                    ContextId = contextId
                };

                context.Add(conversation);
                context.AddRange<ConversationParticipant>(
                [
                    new ConversationParticipant { ConversationId = conversation.Id, UserId = userId },
                    new ConversationParticipant { ConversationId = conversation.Id, UserId = otherUserId }
                ]);

                await context.SaveChangesAsync(cancellationToken);
                return conversation;
            }, IsolationLevel.ReadCommitted, cancellationToken);
        }
        catch (PersistenceException)
        {
            // The failed transaction is already rolled back. Whoever won the race committed the
            // conversation this one tried to create.
            context.Clear();

            var racedConversation = await context.Tracked<Conversation>()
                .SingleOrDefaultAsync(candidate => candidate.ParticipantKey == participantKey && candidate.ContextKey == contextKey, cancellationToken);

            if (racedConversation is null)
            {
                throw;
            }

            return racedConversation;
        }
    }

    private static ClientMessageDto ToClientMessage(Message message) =>
        new(
            message.Id,
            message.ConversationId,
            message.SenderUserId,
            message.Body,
            message.MediaObjectId,
            message.CreatedAt);

    private static string BuildParticipantKey(Guid first, Guid second)
    {
        var firstText = first.ToString("D");
        var secondText = second.ToString("D");
        return string.CompareOrdinal(firstText, secondText) < 0
            ? $"{firstText}:{secondText}"
            : $"{secondText}:{firstText}";
    }
}
