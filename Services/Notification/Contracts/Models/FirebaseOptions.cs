namespace CompanyHQ.Services.Notifications.Contracts.Models;

/// <summary>Optional Firebase Cloud Messaging credentials and project identity.</summary>
public sealed class FirebaseOptions
{
    public const string SectionName = "Firebase";

    /// <summary>The Firebase project ID, not the display name.</summary>
    public string ProjectId { get; set; } = string.Empty;

    /// <summary>A service-account file path or the raw service-account JSON.</summary>
    public string ServiceAccountJson { get; set; } = string.Empty;

    /// <summary>
    /// Validates values that are present while allowing an entirely unconfigured development host
    /// to degrade to a disabled push transport.
    /// </summary>
    public void Validate()
    {
        if (!string.IsNullOrWhiteSpace(ProjectId)
            && (ProjectId.Length is < 6 or > 30
                || !char.IsAsciiLetter(ProjectId[0])
                || !char.IsAsciiLetterOrDigit(ProjectId[^1])
                || ProjectId.Any(character =>
                    !((character is >= 'a' and <= 'z') || char.IsAsciiDigit(character) || character == '-'))))
        {
            throw new InvalidOperationException(
                $"{SectionName}:ProjectId must be a lowercase Firebase project ID.");
        }

        if (ServiceAccountJson is { Length: > 16 * 1024 * 1024 })
        {
            throw new InvalidOperationException(
                $"{SectionName}:ServiceAccountJson is larger than the supported service-account limit.");
        }
    }
}
