using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

using CompanyHQ.Geo.Contracts.Ports;
using CompanyHQ.OpenStreetMap.Contracts.Models;
using CompanyHQ.OpenStreetMap.Services;

namespace CompanyHQ.OpenStreetMap.Extensions;

/// <summary>
/// Single registration entry point for the CompanyHQ OpenStreetMap adapter.
/// </summary>
public static class OpenStreetMapServiceCollectionExtensions
{
    /// <summary>
    /// Registers the Nominatim and Overpass clients and fills Geo's <see cref="IGeoProvider"/> with them.
    /// </summary>
    public static IServiceCollection AddOpenStreetMap(this IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        var options = configuration.GetSection(OpenStreetMapOptions.SectionName).Get<OpenStreetMapOptions>() ?? new();

        // A missing User-Agent gets every request blocked by Nominatim. Better to fail the host
        // than to ship a build whose map silently never resolves.
        options.Validate();

        services.AddSingleton(options);

        services.AddSingleton<NominatimRateLimiter>();
        services.AddSingleton<OverpassRateLimiter>();
        services.AddTransient<GeoAdmissionHandler>();

        services.AddHttpClient<NominatimClient>(http =>
        {
            http.BaseAddress = new Uri(options.BaseAddress);
            http.DefaultRequestHeaders.UserAgent.ParseAdd(options.UserAgent);

            // Nominatim occasionally stalls under load; a lookup that outlives the page it feeds is
            // worse than no area at all.
            http.Timeout = TimeSpan.FromSeconds(10);
        }).AddHttpMessageHandler<GeoAdmissionHandler>();

        services.AddHttpClient<OverpassClient>(http =>
        {
            http.BaseAddress = new Uri(options.OverpassBaseAddress);
            http.DefaultRequestHeaders.UserAgent.ParseAdd(options.UserAgent);

            // Above the query's own budget, so Overpass gets to answer 504 itself instead of the
            // request being cancelled here with no idea whether it was slow or broken.
            http.Timeout = TimeSpan.FromSeconds(OverpassClient.QueryTimeoutSeconds + 10);
        }).AddHttpMessageHandler<GeoAdmissionHandler>();

        services.AddScoped<IGeoProvider, OpenStreetMapGeoProvider>();

        return services;
    }
}
