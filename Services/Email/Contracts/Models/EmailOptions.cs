namespace CompanyHQ.Email.Contracts.Models;

public sealed class EmailOptions
{
    public const string SectionName = "Email";

    public string From { get; set; } = string.Empty;

    public int MaxSendsPerSecond { get; set; } = 10;

    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(From) || !From.Contains('@', StringComparison.Ordinal))
        {
            throw new InvalidOperationException("Email:From must be a verified sender address.");
        }

        if (MaxSendsPerSecond < 1)
        {
            throw new InvalidOperationException("Email:MaxSendsPerSecond must be at least 1.");
        }
    }
}
