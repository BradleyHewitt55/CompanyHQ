namespace CompanyHQ.Services.Messages.Contracts.Dtos;

public sealed record MessagePageDto(
    IReadOnlyList<ClientMessageDto> Messages,
    string? NextCursor,
    bool IsClosed = false,
    DateTimeOffset? ClosedAt = null);
