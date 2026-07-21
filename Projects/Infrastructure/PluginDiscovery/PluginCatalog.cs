using System.Reflection;
using Abstractions.Plugins;

namespace CompanyHQ.Infrastructure.PluginDiscovery;

public sealed class PluginCatalog : IPluginCatalog
{
    private readonly IReadOnlyList<PluginStatus> statuses;

    internal PluginCatalog(
        IReadOnlyList<DiscoveredPlugin> discovered,
        IReadOnlyList<ICompanyHqPlugin> activePlugins)
    {
        statuses = discovered
            .Where(item => item.Plugin.Descriptor.Origin == PluginOrigin.Company)
            .Select(item => new PluginStatus(
                item.Plugin.Descriptor,
                item.Enabled,
                item.AssemblyPath))
            .Distinct()
            .OrderBy(status => status.Descriptor.Name)
            .ToArray();
        RouteAssemblies = activePlugins
            .SelectMany(plugin => plugin.RouteAssemblies)
            .Distinct()
            .ToArray();
    }

    public IReadOnlyList<Assembly> RouteAssemblies { get; }

    public IReadOnlyList<PluginStatus> GetPlugins() => statuses;
}
