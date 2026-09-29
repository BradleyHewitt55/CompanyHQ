namespace CompanyHQ.Services.Resend.Contracts.Models;

public sealed class ResendOptions
{
    public const string SectionName = "Resend";

    public string ApiKey { get; set; } = string.Empty;

    public string From { get; set; } = string.Empty;

    public int MaxRequestsPerSecond { get; set; } = 10;

    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(ApiKey) || !ApiKey.StartsWith("re_", StringComparison.Ordinal))
        {
            throw new InvalidOperationException("Resend:ApiKey must be a Resend API key.");
        }

        if (string.IsNullOrWhiteSpace(From) || !From.Contains('@', StringComparison.Ordinal))
        {
            throw new InvalidOperationException("Resend:From must be a verified sender address.");
        }

        if (MaxRequestsPerSecond < 1)
        {
            throw new InvalidOperationException("Resend:MaxRequestsPerSecond must be at least 1.");
        }
    }
}
