namespace CompanyHQ.Services.Email.Contracts.Dtos;

/// <summary>The administrator-only view of a controlled transactional email template.</summary>
public sealed record AdminEmailTemplateDto(
    string Key,
    string DisplayName,
    string Html,
    IReadOnlyList<string> SupportedPlaceholders,
    IReadOnlyList<string> RequiredPlaceholders,
    DateTimeOffset? UpdatedAt,
    Guid? UpdatedByUserId,
    bool IsCustomized);
