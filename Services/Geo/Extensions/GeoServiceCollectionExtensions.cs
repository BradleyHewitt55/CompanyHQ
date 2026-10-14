using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

using CompanyHQ.Geo.Contracts.Models;
using CompanyHQ.Geo.Services;

namespace CompanyHQ.Geo.Extensions;

/// <summary>Registers the Geo service and its controller assembly.</summary>
public static class GeoServiceCollectionExtensions
{
    /// <summary>
    /// Registers area lookups and the nearby-place map. Call after <c>AddCache</c>;
    /// <see cref="GeoService"/> depends on that registration's <c>ICacheService</c>. The host
    /// registers the provider adapter that fills <c>IGeoProvider</c> beside it; without one the
    /// host fails validation at startup.
    /// </summary>
    public static IServiceCollection AddGeo(this IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        var options = configuration.GetSection(GeoOptions.SectionName).Get<GeoOptions>() ?? new();

        // Better to fail the host than to ship a build whose map quietly evicts or over-fetches.
        options.Validate();

        services.AddSingleton(options);

        // Singleton: it de-duplicates in-flight refreshes across requests, which a scoped instance
        // could not see.
        services.AddSingleton<NearbyTileRefresher>();
        services.AddScoped<NearbyTileStore>();
        services.AddScoped<IGeoService, GeoService>();
        services.AddRateLimiter(rateLimiter =>
            rateLimiter.AddPolicy<string, GeoRateLimitPolicy>(GeoOptions.InboundRateLimitPolicyName));
        services.AddControllers().AddApplicationPart(typeof(GeoServiceCollectionExtensions).Assembly);

        return services;
    }
}
