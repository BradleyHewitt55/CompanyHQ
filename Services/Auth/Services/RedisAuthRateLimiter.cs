using System.Security.Cryptography;
using System.Text;

using CompanyHQ.Cache.Services;

namespace CompanyHQ.Auth.Services;

/// <summary>Fixed-window limiting backed by the shared Redis cache, namespaced by purpose.</summary>
public sealed class RedisAuthRateLimiter(ICacheService cache) : IAuthRateLimiter
{
    public async Task<AuthRateLimitDecision> CheckAsync(
        string purpose,
        string key,
        int maxAttempts,
        TimeSpan window,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(purpose);
        ArgumentException.ThrowIfNullOrWhiteSpace(key);

        var counter = await cache.IncrementAsync($"auth:{purpose}:{Digest(key)}", window, cancellationToken);

        if (!counter.IsAvailable)
        {
            // A security counter outage must not silently turn into unlimited attempts.
            return AuthRateLimitDecision.Block(window);
        }

        return counter.Count <= maxAttempts
            ? AuthRateLimitDecision.Permit
            : AuthRateLimitDecision.Block(counter.RetryAfter);
    }

    private static string Digest(string value) =>
        Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(value)));
}
