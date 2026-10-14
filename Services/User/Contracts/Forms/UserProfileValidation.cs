using System.ComponentModel.DataAnnotations;

namespace CompanyHQ.Users.Contracts.Forms;

/// <summary>Validation shared by self-service profile edits.</summary>
public static class UserProfileValidation
{
    /// <summary>Allows no image, but never accepts an insecure or malformed image URL.</summary>
    public static IEnumerable<ValidationResult> ValidateProfileImageUrl(string? profileImageUrl)
    {
        if (string.IsNullOrWhiteSpace(profileImageUrl))
        {
            return [];
        }

        return Uri.TryCreate(profileImageUrl.Trim(), UriKind.Absolute, out var uri) && IsServable(uri)
            ? []
            : [new ValidationResult(
                "Profile image must be an HTTPS URL, or a loopback HTTP URL in local development.",
                ["ProfileImageUrl"])];
    }

    /// <summary>
    /// HTTPS everywhere that matters, plus plain HTTP to a loopback host. Local development serves
    /// media from the API itself over HTTP, and rejecting that makes every photo save fail with a
    /// message about transport security that has nothing to do with the photo.
    /// </summary>
    private static bool IsServable(Uri uri) =>
        uri.Scheme == Uri.UriSchemeHttps
        || (uri.Scheme == Uri.UriSchemeHttp && uri.IsLoopback);
}
