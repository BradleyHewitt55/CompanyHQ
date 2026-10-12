using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using CompanyHQ.Services.Cache.Contracts.Models;
using CompanyHQ.Services.Cache.Contracts.Ports;
using CompanyHQ.Services.Cache.Services;

namespace CompanyHQ.Services.Cache.Extensions;

/// <summary>
/// Single registration entry point for the CompanyHQ Cache service.
/// </summary>
public static class CacheServiceCollectionExtensions
{
    /// <summary>
    /// Registers <see cref="ICacheService"/> and the <c>cache</c> health check. The host registers
    /// the provider adapter that fills <see cref="ICacheProvider"/> beside it; without one the host
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
        services.AddSingleton<ICacheService, CacheService>();

        // Added when the health options are first built rather than here, so the entry carries the
        // composed provider's own tag whichever of the two registrations the host calls first.
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
