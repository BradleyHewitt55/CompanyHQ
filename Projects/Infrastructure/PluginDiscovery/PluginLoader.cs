using Abstractions.Plugins;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace CompanyHQ.Infrastructure.PluginDiscovery;

internal sealed record PluginRuntime(IReadOnlyList<ICompanyHqPlugin> ActivePlugins);

public static class PluginLoader
{
    public static IServiceCollection AddPluginDiscovery(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var discovered = PluginAssemblyScanner.Scan(configuration);
        var activeById = new Dictionary<string, ICompanyHqPlugin>(StringComparer.OrdinalIgnoreCase);

        foreach (var item in discovered.Where(item => item.Enabled))
        {
            if (activeById.TryGetValue(item.Plugin.Descriptor.Id, out var existing)
                && existing.GetType().Assembly != item.Plugin.GetType().Assembly)
            {
                throw new InvalidOperationException(
                    $"Plugin id '{item.Plugin.Descriptor.Id}' is supplied by multiple assemblies.");
            }

            activeById[item.Plugin.Descriptor.Id] = item.Plugin;
        }

        var activePlugins = PluginDependencyValidator.Order(activeById.Values);
        foreach (var plugin in activePlugins)
        {
            plugin.ConfigureServices(services, configuration);
        }

        var catalog = new PluginCatalog(discovered, activePlugins);
        services.AddSingleton<IPluginCatalog>(catalog);
        services.AddSingleton(new PluginRuntime(activePlugins));
        return services;
    }

    public static IApplicationBuilder UseDiscoveredPlugins(this IApplicationBuilder application)
    {
        var runtime = application.ApplicationServices.GetRequiredService<PluginRuntime>();
        foreach (var plugin in runtime.ActivePlugins)
        {
            plugin.ConfigureApplication(application);
        }

        return application;
    }
}
