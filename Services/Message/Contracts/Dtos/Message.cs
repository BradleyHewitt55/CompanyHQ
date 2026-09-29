namespace CompanyHQ.Services.Messages.Contracts.Dtos;

/// <summary>
/// The participant-facing message shape. Suppression is deliberately not represented on the wire.
/// </summary>
public sealed record ClientMessageDto(
    Guid Id,
    Guid ConversationId,
    Guid SenderUserId,
    string Body,
    Guid? MediaObjectId,
    DateTimeOffset CreatedAt);
