using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using CompanyHQ.Cache.Contracts.Models;
using CompanyHQ.Cache.Contracts.Ports;
using CompanyHQ.Cache.Services;

namespace CompanyHQ.Cache.Extensions;

/// <summary>
/// Single registration entry point for the CompanyHQ Cache service.
/// </summary>
public static class CacheServiceCollectionExtensions
{
    /// <summary>
    /// Registers <see cref="ICacheService"/> over Redis and the <c>cache</c> health check. The host
    /// registers its StackExchange.Redis <c>IConnectionMultiplexer</c> first; without it the host
    /// fails validation at startup.
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
        services.AddSingleton<ICacheProvider, RedisCacheProvider>();
        services.AddSingleton<ICacheService, CacheService>();

        // Added when the health options are first built rather than here, so the entry carries the
        // tag of the provider that is actually resolved.
        services.AddHealthChecks();
        services.AddOptions<HealthCheckServiceOptions>().Configure<ICacheProvider>((health, provider) =>
            health.Registrations.Add(new HealthCheckRegistration(
                CacheHealthCheck.Name,
                _ => new CacheHealthCheck(provider),
                failureStatus: HealthStatus.Degraded,
                tags: ["cache", provider.Name])));

        return services;
    }
}
