namespace CompanyHQ.GitHub.Contracts.Models;

/// <summary>Optional. Without a key the host still starts and connecting a repository is refused.</summary>
public sealed class GitHubOptions
{
    public const string SectionName = "GitHub";

    /// <summary>A base64-encoded 32-byte key. Changing it strands every stored token.</summary>
    public string? TokenEncryptionKey { get; set; }

    public bool IsConfigured => !string.IsNullOrWhiteSpace(TokenEncryptionKey);

    public byte[] Key() => Convert.FromBase64String(TokenEncryptionKey!);

    public void Validate()
    {
        if (!IsConfigured)
        {
            return;
        }

        try
        {
            if (Key().Length == 32)
            {
                return;
            }
        }
        catch (FormatException)
        {
        }

        throw new InvalidOperationException("GitHub:TokenEncryptionKey must be a base64-encoded 32-byte key.");
    }
}
