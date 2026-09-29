namespace CompanyHQ.Services.Media.Contracts.Dtos;

/// <summary>Short-lived permission for a client to upload bytes directly to object storage.</summary>
public sealed record MediaUploadTicketDto(
    Guid MediaObjectId,
    string UploadUrl,
    string? Url,
    DateTimeOffset ExpiresAt);
