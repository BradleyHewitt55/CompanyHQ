using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;

using CompanyHQ.Messages.Contracts.Dtos;
using CompanyHQ.Messages.Services;

namespace CompanyHQ.Messages.Hubs;

/// <summary>Best-effort SignalR delivery after the database transaction has committed.</summary>
public sealed class SignalRMessageBroadcaster(
    IHubContext<MessageHub> hub,
    ILogger<SignalRMessageBroadcaster> logger) : IMessageBroadcaster
{
    public Task BroadcastMessageAsync(
        ClientMessageDto message,
        Guid senderUserId,
        Guid recipientUserId,
        CancellationToken cancellationToken = default) =>
        SendToUsersAsync(
            "MessageReceived",
            message,
            [senderUserId, recipientUserId],
            cancellationToken);

    public Task BroadcastReadAsync(
        Guid conversationId,
        DateTimeOffset readAt,
        Guid readerUserId,
        Guid otherParticipantId,
        CancellationToken cancellationToken = default) =>
        SendReadToUsersAsync(
            conversationId,
            readAt,
            [readerUserId, otherParticipantId],
            cancellationToken);

    private async Task SendReadToUsersAsync(
        Guid conversationId,
        DateTimeOffset readAt,
        IReadOnlyList<Guid> userIds,
        CancellationToken cancellationToken)
    {
        try
        {
            await Task.WhenAll(userIds
                .Distinct()
                .Select(userId => hub.Clients
                    .Group(MessageHub.UserGroup(userId))
                    .SendAsync("ConversationRead", conversationId, readAt, cancellationToken)));
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            logger.LogDebug("SignalR ConversationRead delivery was cancelled.");
        }
        catch (Exception exception)
        {
            logger.LogWarning(exception, "SignalR ConversationRead delivery failed.");
        }
    }

    private async Task SendToUsersAsync<T>(
        string eventName,
        T payload,
        IReadOnlyList<Guid> userIds,
        CancellationToken cancellationToken)
    {
        try
        {
            await Task.WhenAll(userIds
                .Distinct()
                .Select(userId => hub.Clients
                    .Group(MessageHub.UserGroup(userId))
                    .SendAsync(eventName, payload, cancellationToken)));
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            logger.LogDebug("SignalR {EventName} delivery was cancelled.", eventName);
        }
        catch (Exception exception)
        {
            // The database is the source of truth; clients refetch after reconnect.
            logger.LogWarning(exception, "SignalR {EventName} delivery failed.", eventName);
        }
    }
}
