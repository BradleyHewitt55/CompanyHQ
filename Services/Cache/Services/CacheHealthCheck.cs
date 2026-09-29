using Microsoft.Extensions.Diagnostics.HealthChecks;
using StackExchange.Redis;

namespace CompanyHQ.Services.Cache.Services;

/// <summary>
/// Pings Redis through the shared multiplexer.
/// </summary>
/// <remarks>
/// Degraded rather than unhealthy: the app serves traffic without a cache, so a Redis outage must
/// not fail a readiness probe and pull the host out of rotation.
/// </remarks>
public sealed class CacheHealthCheck(IConnectionMultiplexer connection) : IHealthCheck
{
    public const string Name = "cache";

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var latency = await connection.GetDatabase().PingAsync();

            return HealthCheckResult.Healthy(
                "Redis is reachable.",
                new Dictionary<string, object> { ["latencyMs"] = latency.TotalMilliseconds });
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            return new HealthCheckResult(
                context.Registration.FailureStatus,
                "Redis is unreachable.",
                exception);
        }
    }
}
