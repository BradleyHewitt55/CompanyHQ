namespace CompanyHQ.Auth.Services;

/// <summary>
/// The fixed limits behind every credential endpoint's rate limiting. Policy decisions, not
/// deployment knobs — unlike <c>LoginRateLimitOptions</c>, these are not configurable.
/// </summary>
internal static class AuthRateLimits
{
    /// <summary>Registration creates an account and sends mail, so both its identity and IP budgets are fixed.</summary>
    public const int RegistrationIdentityMaxAttempts = 3;

    public const int RegistrationIpMaxAttempts = 10;

    public static readonly TimeSpan RegistrationWindow = TimeSpan.FromMinutes(15);

    /// <summary>Each attempt sends mail; the limit protects a stranger's inbox, not just the database.</summary>
    public const int ResendVerificationMaxAttempts = 3;

    public static readonly TimeSpan ResendVerificationWindow = TimeSpan.FromMinutes(15);

    /// <summary>Same mailbox-flood reasoning as resend-verification.</summary>
    public const int ForgotPasswordIdentityMaxAttempts = 3;

    /// <summary>Wider than the identity limit: one IP guessing many addresses is a different attack.</summary>
    public const int ForgotPasswordIpMaxAttempts = 20;

    public static readonly TimeSpan ForgotPasswordWindow = TimeSpan.FromMinutes(15);

    /// <summary>Authenticated, so partitioned by the principal, never by IP alone.</summary>
    public const int ChangeCredentialMaxAttempts = 5;

    public static readonly TimeSpan ChangeCredentialWindow = TimeSpan.FromMinutes(15);
}
