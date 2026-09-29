using CompanyHQ.Services.Messages.Contracts.Dtos;

namespace CompanyHQ.Services.Messages.Services;

/// <summary>Delivery seam kept outside the persistence service so the hub remains write-free.</summary>
public interface IMessageBroadcaster
{
    Task BroadcastMessageAsync(
        ClientMessageDto message,
        Guid senderUserId,
        Guid recipientUserId,
        CancellationToken cancellationToken = default);

    Task BroadcastReadAsync(
        Guid conversationId,
        DateTimeOffset readAt,
        Guid readerUserId,
        Guid otherParticipantId,
        CancellationToken cancellationToken = default);
}
