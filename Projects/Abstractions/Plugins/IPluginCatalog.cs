using System.Reflection;

namespace Abstractions.Plugins;

public interface IPluginCatalog
{
    IReadOnlyList<PluginStatus> GetPlugins();
    IReadOnlyList<Assembly> RouteAssemblies { get; }
}
