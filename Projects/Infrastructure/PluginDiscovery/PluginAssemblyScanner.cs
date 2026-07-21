using System.Reflection;
using System.Runtime.Loader;
using System.Text.Json;
using Abstractions.Plugins;
using Microsoft.Extensions.Configuration;

namespace CompanyHQ.Infrastructure.PluginDiscovery;

internal sealed record DiscoveredPlugin(
    ICompanyHqPlugin Plugin,
    string AssemblyPath,
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
                        plugin.Descriptor.Activation == PluginActivation.Required));
                }
            }
        }

        // The process is bound to one company at startup; another tenant's services are
        // never scanned, so a rebuild in one company cannot reach this host.
        var company = configuration["CompanyHq:Company"];
        if (string.IsNullOrWhiteSpace(company)
            || company.Length > 64
            || !company.All(character => char.IsLetterOrDigit(character) || character is ' ' or '-' or '_'))
        {
            throw new InvalidOperationException(
                "CompanyHq:Company must be set to 1-64 letters, digits, spaces, dashes, or underscores.");
        }

        var serverRoot = configuration["CompanyHq:ServerRoot"]
            ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "CompanyHQ");
        // Containers keep immutable plugin binaries outside mutable company state so a
        // runtime mount cannot shadow the assemblies baked into the image.
        var pluginRoot = configuration["CompanyHq:PluginRoot"] ?? serverRoot;
        var servicesDirectory = Path.Combine(Path.GetFullPath(pluginRoot), "Companies", company, "Services");
        if (!Directory.Exists(servicesDirectory))
        {
            return discovered;
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
                discovered.Add(new DiscoveredPlugin(plugin, assemblyPath, enabled));
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
