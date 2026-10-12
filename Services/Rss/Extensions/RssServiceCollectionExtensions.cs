using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using CompanyHQ.Services.Rss.Contracts.Models;
using CompanyHQ.Services.Rss.Contracts.Ports;
using CompanyHQ.Services.Rss.Services;

namespace CompanyHQ.Services.Rss.Extensions;

/// <summary>
/// Single registration entry point for the CompanyHQ RSS service.
/// </summary>
public static class RssServiceCollectionExtensions
{
    /// <summary>
    /// Registers <see cref="IRssService"/> for in-process callers. It maps no endpoint: reading an
    /// address chosen by a caller is something a service does on its own behalf, never a public route.
    /// </summary>
    public static IServiceCollection AddRss(this IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        var options = configuration.GetSection(RssOptions.SectionName).Get<RssOptions>() ?? new();

        // A limit that binds to nonsense is a missing limit; refuse to start rather than fetch unbounded.
        options.Validate();

        services.AddSingleton(options);
        services.AddSingleton<IRssNetwork, SystemRssNetwork>();

        // Singleton: it owns the connection pool, and holds no per-caller state.
        services.AddSingleton<IRssService, RssService>();

        return services;
    }
}
