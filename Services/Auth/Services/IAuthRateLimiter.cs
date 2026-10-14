namespace CompanyHQ.Auth.Services;

/// <summary>
/// Distributed protection for anonymous and authenticated credential endpoints — register, login,
/// forgot-password, reset-password, resend-verification, change-password, change-email.
/// </summary>
/// <remarks>
/// One dimension per call. Endpoints that partition by more than one dimension (login checks IP and
/// normalized identity; forgot-password checks both too, with different limits on each) call this
/// once per dimension and combine the decisions themselves, since the dimensions and their limits
/// differ per endpoint and there is no single shape that fits all of them.
/// </remarks>
public interface IAuthRateLimiter
{
    Task<AuthRateLimitDecision> CheckAsync(
        string purpose,
        string key,
        int maxAttempts,
        TimeSpan window,
        CancellationToken cancellationToken = default);
}
