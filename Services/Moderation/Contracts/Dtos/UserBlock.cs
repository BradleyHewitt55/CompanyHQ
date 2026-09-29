namespace CompanyHQ.Services.Moderation.Contracts.Dtos;

/// <summary>The caller's outbound block without the private reason they supplied.</summary>
public sealed record ClientUserBlockDto(
    Guid BlockedUserId,
    string? FirstName,
    string? LastName,
    DateTimeOffset CreatedAt);
