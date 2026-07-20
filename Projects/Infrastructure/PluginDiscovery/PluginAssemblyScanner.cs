using System.Reflection;
using System.Runtime.Loader;
using System.Text.Json;
using Abstractions.Plugins;
using Microsoft.Extensions.Configuration;

namespace CompanyHQ.Infrastructure.PluginDiscovery;

internal sealed record DiscoveredPlugin(
    ICompanyHqPlugin Plugin,
    string AssemblyPath,
    string? Company,
    bool Enabled);

internal static class PluginAssemblyScanner
{
    public static IReadOnlyList<DiscoveredPlugin> Scan(IConfiguration configuration)
    {
        var discovered = new List<DiscoveredPlugin>();
        var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var path in Directory.EnumerateFiles(AppContext.BaseDirectory, "*.dll"))
        {
            foreach (var plugin in Load(path, visited))
            {
                if (plugin.Descriptor.Origin == PluginOrigin.BuiltIn)
                {
                    discovered.Add(new DiscoveredPlugin(
                        plugin,
                        path,
                        null,
                        plugin.Descriptor.Activation == PluginActivation.Required));
                }
            }
        }

        var serverRoot = configuration["CompanyHq:ServerRoot"]
            ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "CompanyHQ");
        var companiesRoot = Path.Combine(Path.GetFullPath(serverRoot), "Companies");
        if (!Directory.Exists(companiesRoot))
        {
            return discovered;
        }

        foreach (var companyDirectory in Directory.EnumerateDirectories(companiesRoot))
        {
            var company = Path.GetFileName(companyDirectory);
            var servicesDirectory = Path.Combine(companyDirectory, "Services");
            if (!Directory.Exists(servicesDirectory))
            {
                continue;
            }

            foreach (var serviceDirectory in Directory.EnumerateDirectories(servicesDirectory))
            {
                var enabled = ReadEnabled(Path.Combine(serviceDirectory, "Settings", "service.json"));
                var assemblyPath = FindServiceAssembly(serviceDirectory);
                if (assemblyPath is null)
                {
                    if (enabled)
                    {
                        throw new InvalidOperationException(
                            $"Enabled service '{Path.GetFileName(serviceDirectory)}' for company '{company}' has no built assembly.");
                    }

                    continue;
                }

                var plugins = Load(assemblyPath, visited).ToArray();
                if (enabled && plugins.Length == 0)
                {
                    throw new InvalidOperationException(
                        $"Enabled service assembly '{assemblyPath}' contains no ICompanyHqPlugin implementation.");
                }

                foreach (var plugin in plugins)
                {
                    discovered.Add(new DiscoveredPlugin(plugin, assemblyPath, company, enabled));
                }
            }
        }

        return discovered;
    }

    private static IEnumerable<ICompanyHqPlugin> Load(string path, HashSet<string> visited)
    {
        path = Path.GetFullPath(path);
        if (!visited.Add(path))
        {
            return [];
        }

        Assembly assembly;
        try
        {
            AssemblyName.GetAssemblyName(path);
            assembly = AssemblyLoadContext.Default.LoadFromAssemblyPath(path);
        }
        catch (BadImageFormatException)
        {
            return [];
        }
        catch (FileLoadException exception)
        {
            assembly = AppDomain.CurrentDomain.GetAssemblies()
                .FirstOrDefault(candidate => string.Equals(candidate.Location, path, StringComparison.OrdinalIgnoreCase))
                ?? throw new InvalidOperationException($"Could not load plugin assembly '{path}'.", exception);
        }

        Type[] types;
        try
        {
            types = assembly.GetTypes();
        }
        catch (ReflectionTypeLoadException exception)
        {
            types = exception.Types.Where(type => type is not null).Cast<Type>().ToArray();
        }

        return types
            .Where(type => typeof(ICompanyHqPlugin).IsAssignableFrom(type)
                && type is { IsClass: true, IsAbstract: false }
                && type.GetConstructor(Type.EmptyTypes) is not null)
            .Select(type => (ICompanyHqPlugin)Activator.CreateInstance(type)!);
    }

    private static bool ReadEnabled(string manifestPath)
    {
        if (!File.Exists(manifestPath))
        {
            return false;
        }

        using var document = JsonDocument.Parse(File.ReadAllText(manifestPath));
        return document.RootElement.TryGetProperty("enabled", out var enabled) && enabled.GetBoolean();
    }

    private static string? FindServiceAssembly(string serviceDirectory)
    {
        var serviceName = Path.GetFileName(serviceDirectory);
        return Directory.EnumerateFiles(serviceDirectory, $"{serviceName}.dll", SearchOption.AllDirectories)
            .Where(path => !path.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(File.GetLastWriteTimeUtc)
            .FirstOrDefault();
    }
}
