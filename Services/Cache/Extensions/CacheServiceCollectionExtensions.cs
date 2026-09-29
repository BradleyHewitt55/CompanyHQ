using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using CompanyHQ.Services.Cache.Contracts.Models;
using CompanyHQ.Services.Cache.Services;
using StackExchange.Redis;

namespace CompanyHQ.Services.Cache.Extensions;

/// <summary>
/// Single registration entry point for the CompanyHQ Cache service.
/// </summary>
public static class CacheServiceCollectionExtensions
{
    /// <summary>
    /// Registers the shared Redis multiplexer, <see cref="ICacheService"/>, and the <c>cache</c>
    /// health check.
    /// </summary>
    public static IServiceCollection AddCache(this IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        var options = configuration.GetSection(CacheOptions.SectionName).Get<CacheOptions>() ?? new();

        // Fail during startup rather than on the first cache read, where the stack trace is useless.
        options.Validate();

        services.AddSingleton(options);
        services.AddSingleton<CacheSerializer>();

        // Connecting lazily keeps a cold Redis from blocking host startup; AbortOnConnectFail is
        // off, so the multiplexer reconnects on its own once Redis is up.
        services.AddSingleton<IConnectionMultiplexer>(_ =>
            ConnectionMultiplexer.Connect(options.BuildConfiguration()));

        services.AddSingleton<ICacheService, CacheService>();

        services.AddHealthChecks().AddCheck<CacheHealthCheck>(
            CacheHealthCheck.Name,
            failureStatus: HealthStatus.Degraded,
            tags: ["cache", "redis"]);

        return services;
    }
}
