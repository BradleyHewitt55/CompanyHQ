namespace CompanyHQ.Services.Auth.Contracts.Models;

/// <summary>Distributed login-attempt limits.</summary>
public sealed class LoginRateLimitOptions
{
    public const string SectionName = "Auth:LoginRateLimit";

    public int MaxAttempts { get; set; } = 10;

    public int WindowSeconds { get; set; } = 300;

    public TimeSpan Window => TimeSpan.FromSeconds(WindowSeconds);

    public void Validate()
    {
        if (MaxAttempts <= 0)
        {
            throw new InvalidOperationException("Auth:LoginRateLimit:MaxAttempts must be greater than zero.");
        }

        if (WindowSeconds <= 0)
        {
            throw new InvalidOperationException("Auth:LoginRateLimit:WindowSeconds must be greater than zero.");
        }
    }
}
