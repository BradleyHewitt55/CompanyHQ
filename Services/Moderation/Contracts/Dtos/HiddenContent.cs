namespace CompanyHQ.Services.Moderation.Contracts.Dtos;

/// <summary>A target hidden from the caller, with no content or author data attached.</summary>
public sealed record ClientHiddenContentDto(string TargetKind, Guid TargetId);
