using Microsoft.Extensions.Diagnostics.HealthChecks;
using CompanyHQ.Cache.Contracts.Ports;

namespace CompanyHQ.Cache.Services;

/// <summary>
/// The <c>cache</c> health entry: a round trip through the provider.
/// </summary>
/// <remarks>
/// Degraded rather than unhealthy: the app serves traffic without a cache, so a cache outage must
/// not fail a readiness probe and pull the host out of rotation.
/// </remarks>
public sealed class CacheHealthCheck(ICacheProvider provider) : IHealthCheck
{
    public const string Name = "cache";

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var latency = await provider.PingAsync(cancellationToken);

            return HealthCheckResult.Healthy(
                "The cache is reachable.",
                new Dictionary<string, object> { ["latencyMs"] = latency.TotalMilliseconds });
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            return new HealthCheckResult(
                context.Registration.FailureStatus,
                "The cache is unreachable.",
                exception);
        }
    }
}
