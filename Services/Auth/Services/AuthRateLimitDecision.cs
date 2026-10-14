namespace CompanyHQ.Auth.Services;

/// <summary>Whether an attempt may continue and, when blocked, how long to wait.</summary>
public readonly record struct AuthRateLimitDecision(bool Allowed, TimeSpan RetryAfter)
{
    public static AuthRateLimitDecision Permit => new(true, TimeSpan.Zero);

    public static AuthRateLimitDecision Block(TimeSpan retryAfter) => new(false, retryAfter);
}
