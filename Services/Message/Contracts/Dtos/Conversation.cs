namespace CompanyHQ.Services.Messages.Contracts.Dtos;

/// <summary>One conversation as seen by the requesting participant.</summary>
public sealed record ClientConversationDto(
    Guid Id,
    Guid OtherParticipantId,
    string? OtherParticipantFirstName,
    string? OtherParticipantLastName,
    DateTimeOffset? LastMessageAt,
    string? LastMessagePreview,
    int UnreadCount,
    bool IsClosed = false,
    DateTimeOffset? ClosedAt = null,
    string? ContextKind = null,
    string? ContextId = null);

/// <summary>Closed conversation history as seen by an administrator.</summary>
public sealed record AdminConversationDto(
    Guid Id,
    IReadOnlyList<AdminConversationParticipantDto> Participants,
    DateTimeOffset? LastMessageAt,
    string? LastMessagePreview,
    DateTimeOffset ClosedAt,
    Guid? ClosedByUserId,
    int MessageCount,
    string? ContextKind = null,
    string? ContextId = null);

public sealed record AdminConversationParticipantDto(
    Guid UserId,
    string? FirstName,
    string? LastName);
