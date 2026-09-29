namespace CompanyHQ.Services.Company.Contracts.Dtos;

/// <summary>
/// The company profile as everyone sees it. There is no admin variant: the row holds published
/// company information only, and <c>UpdatedAt</c> is the whole audit story an operator needs.
/// </summary>
public sealed record ClientCompanyProfileDto(
    string Name,
    string Website,
    string SupportEmail,
    string InfoEmail,
    string? LogoUrl,
    string Description,
    DateTimeOffset UpdatedAt);
