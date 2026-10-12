using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using CompanyHQ.Services.Cache.Contracts.Ports;
using CompanyHQ.Services.Redis.Contracts.Models;
using CompanyHQ.Services.Redis.Services;
using StackExchange.Redis;

namespace CompanyHQ.Services.Redis.Extensions;

/// <summary>
/// Single registration entry point for the CompanyHQ Redis adapter.
/// </summary>
public static class RedisServiceCollectionExtensions
{
    /// <summary>
    /// Registers the shared Redis multiplexer and fills Cache's <see cref="ICacheProvider"/> with it.
    /// </summary>
    public static IServiceCollection AddRedis(this IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        var options = configuration.GetSection(RedisOptions.SectionName).Get<RedisOptions>() ?? new();

        // Fail during startup rather than on the first cache read, where the stack trace is useless.
        options.Validate();

        services.AddSingleton(options);

        // Connecting lazily keeps a cold Redis from blocking host startup; AbortOnConnectFail is
        // off, so the multiplexer reconnects on its own once Redis is up.
        services.AddSingleton<IConnectionMultiplexer>(_ =>
            ConnectionMultiplexer.Connect(options.BuildConfiguration()));

        services.AddSingleton<ICacheProvider, RedisCacheProvider>();

        return services;
    }
}
