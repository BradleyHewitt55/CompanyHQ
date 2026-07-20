using Abstractions.Plugins;

namespace CompanyHQ.Infrastructure.PluginDiscovery;

internal static class PluginDependencyValidator
{
    public static IReadOnlyList<ICompanyHqPlugin> Order(IEnumerable<ICompanyHqPlugin> plugins)
    {
        var byId = plugins.ToDictionary(plugin => plugin.Descriptor.Id, StringComparer.OrdinalIgnoreCase);
        var ordered = new List<ICompanyHqPlugin>();
        var visiting = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var plugin in byId.Values)
        {
            Visit(plugin);
        }

        return ordered;

        void Visit(ICompanyHqPlugin plugin)
        {
            if (visited.Contains(plugin.Descriptor.Id))
            {
                return;
            }

            if (!visiting.Add(plugin.Descriptor.Id))
            {
                throw new InvalidOperationException($"Plugin dependency cycle includes '{plugin.Descriptor.Id}'.");
            }

            foreach (var dependencyId in plugin.Descriptor.Dependencies)
            {
                if (!byId.TryGetValue(dependencyId, out var dependency))
                {
                    throw new InvalidOperationException(
                        $"Plugin '{plugin.Descriptor.Id}' requires missing plugin '{dependencyId}'.");
                }

                Visit(dependency);
            }

            visiting.Remove(plugin.Descriptor.Id);
            visited.Add(plugin.Descriptor.Id);
            ordered.Add(plugin);
        }
    }
}
