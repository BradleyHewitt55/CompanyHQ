using System.ComponentModel.DataAnnotations;
using CompanyHQ.Services.Company.Contracts.Dtos;

namespace CompanyHQ.Services.Company.Contracts.Forms;

/// <summary>The full company profile. A save replaces every field, so an omitted optional one is cleared.</summary>
public sealed record UpdateCompanyProfileRequest(
    [Required, MaxLength(160)] string Name,
    [Required, MaxLength(320)] string Website,
    [Required, EmailAddress, MaxLength(320)] string SupportEmail,
    [Required, EmailAddress, MaxLength(320)] string InfoEmail,
    [Required, MaxLength(2_000)] string Description,
    [MaxLength(2048)] string? LogoUrl = null,
    [RegularExpression(@"^[A-Za-z0-9._-]{1,160}$")] string? FacebookAccount = null,
    [RegularExpression(@"^[A-Za-z0-9._]{1,30}$")] string? InstagramAccount = null,
    [RegularExpression(@"^\+?[1-9]\d{6,14}$")] string? WhatsAppNumber = null,
    [MinLength(2), MaxLength(20)] IReadOnlyList<ClientCompanyEmailDto>? PublicEmails = null) : IValidatableObject
{
    public IEnumerable<ValidationResult> Validate(ValidationContext context) =>
        CompanyProfileValidation.ValidateLogoUrl(LogoUrl);
}
