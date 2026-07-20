using System.Reflection;

namespace Abstractions.Plugins;

public interface IPluginCatalog
{
    IReadOnlyList<PluginStatus> GetPlugins(string company);
    IReadOnlyList<Assembly> RouteAssemblies { get; }
}
