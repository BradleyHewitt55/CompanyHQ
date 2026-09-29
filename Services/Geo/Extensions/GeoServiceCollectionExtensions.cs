using System.Net.Http.Headers;

using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

using CompanyHQ.Services.Geo.Contracts.Models;
using CompanyHQ.Services.Geo.Services;

namespace CompanyHQ.Services.Geo.Extensions;

/// <summary>Registers the Geo service, its Nominatim client, and its controller assembly.</summary>
public static class GeoServiceCollectionExtensions
{
    /// <summary>
    /// Registers area lookups against OpenStreetMap/Nominatim. Call after <c>AddCache</c>;
    /// <see cref="GeoService"/> depends on that registration's <c>ICacheService</c>.
    /// </summary>
    public static IServiceCollection AddGeo(this IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        var options = configuration.GetSection(GeoOptions.SectionName).Get<GeoOptions>() ?? new();

        // A missing User-Agent gets every request blocked by Nominatim. Better to fail the host
        // than to ship a build whose map silently never resolves.
        options.Validate();

        services.AddSingleton(options);

        services.AddSingleton<NominatimRateLimiter>();
        services.AddSingleton<OverpassRateLimiter>();

        services.AddHttpClient<NominatimClient>(http =>
        {
            http.BaseAddress = new Uri(options.BaseAddress);
            http.DefaultRequestHeaders.UserAgent.ParseAdd(options.UserAgent);
            http.DefaultRequestHeaders.AcceptLanguage.Add(new StringWithQualityHeaderValue(options.Language));

            // Nominatim occasionally stalls under load; a lookup that outlives the page it feeds is
            // worse than no area at all.
            http.Timeout = TimeSpan.FromSeconds(10);
        });

        services.AddHttpClient<OverpassClient>(http =>
        {
            http.BaseAddress = new Uri(options.OverpassBaseAddress);
            http.DefaultRequestHeaders.UserAgent.ParseAdd(options.UserAgent);

            // Above the query's own budget, so Overpass gets to answer 504 itself instead of the
            // request being cancelled here with no idea whether it was slow or broken.
            http.Timeout = TimeSpan.FromSeconds(OverpassClient.QueryTimeoutSeconds + 10);
        });

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
