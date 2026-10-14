using System.ComponentModel.DataAnnotations;

namespace CompanyHQ.Company.Contracts.Forms;

/// <summary>Validation shared by the administrator company-profile save.</summary>
public static class CompanyProfileValidation
{
    public static IEnumerable<ValidationResult> ValidateLogoUrl(string? logoUrl)
    {
        if (string.IsNullOrWhiteSpace(logoUrl))
        {
            return [];
        }

        var cleaned = logoUrl.Trim();
        var managedAsset = System.Text.RegularExpressions.Regex.IsMatch(
            cleaned,
            "^/company-logo/company-logo\\.(?:jpg|png|svg|webp)$",
            System.Text.RegularExpressions.RegexOptions.CultureInvariant);
        return managedAsset || (Uri.TryCreate(cleaned, UriKind.Absolute, out var uri) && IsServable(uri))
            ? []
            : [new ValidationResult(
                "Logo URL must be the managed company logo path, an HTTPS URL, or a loopback HTTP URL in local development.",
                [nameof(UpdateCompanyProfileRequest.LogoUrl)])];
    }

    private static bool IsServable(Uri uri) =>
        uri.Scheme == Uri.UriSchemeHttps
        || (uri.Scheme == Uri.UriSchemeHttp && uri.IsLoopback);
}
