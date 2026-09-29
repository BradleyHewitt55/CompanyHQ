using System.ComponentModel.DataAnnotations;

namespace CompanyHQ.Services.Company.Contracts.Forms;

/// <summary>The full company profile. A save replaces every required field and may clear the logo.</summary>
public sealed record UpdateCompanyProfileRequest(
    [Required, MaxLength(160)] string Name,
    [Required, MaxLength(320)] string Website,
    [Required, EmailAddress, MaxLength(320)] string SupportEmail,
    [Required, EmailAddress, MaxLength(320)] string InfoEmail,
    [Required, MaxLength(2_000)] string Description,
    [MaxLength(2048)] string? LogoUrl = null) : IValidatableObject
{
    public IEnumerable<ValidationResult> Validate(ValidationContext context) =>
        CompanyProfileValidation.ValidateLogoUrl(LogoUrl);
}
