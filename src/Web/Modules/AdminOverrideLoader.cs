using System.Reflection;
using System.Runtime.Loader;
using System.Text.RegularExpressions;
using Services.Companies.Contracts;
using Microsoft.AspNetCore.Components;

namespace Web.Modules;

public static partial class AdminOverrideLoader
{
    public static IReadOnlyList<(string Name, Type Component)> Load(string? assemblyPath, ComponentRegistry registry, LayoutRegistry layouts, ILogger logger)
    {
        var registrations = Load(assemblyPath, registry, logger);
        if (string.IsNullOrWhiteSpace(assemblyPath) || !File.Exists(assemblyPath)) return registrations;
        var assembly = AssemblyLoadContext.Default.Assemblies.FirstOrDefault(candidate => !string.IsNullOrWhiteSpace(candidate.Location) && Path.GetFullPath(candidate.Location).Equals(Path.GetFullPath(assemblyPath), StringComparison.OrdinalIgnoreCase));
        if (assembly is null) return registrations;
        foreach (var layout in assembly.GetExportedTypes().Where(type => !type.IsAbstract && typeof(LayoutComponentBase).IsAssignableFrom(type)))
        {
            layouts.Register(layout.Name, layout);
            logger.LogInformation("Layout override {Name} registered from {Component}.", layout.Name, layout.FullName);
        }
        return registrations;
    }

    public static IReadOnlyList<(string Name, Type Component)> Load(string? assemblyPath, ComponentRegistry registry, ILogger logger)
    {
        if (string.IsNullOrWhiteSpace(assemblyPath) || !File.Exists(assemblyPath)) return [];
        try
        {
            var fullPath = Path.GetFullPath(assemblyPath);
            var assembly = AssemblyLoadContext.Default.Assemblies.FirstOrDefault(candidate =>
                !string.IsNullOrWhiteSpace(candidate.Location) && Path.GetFullPath(candidate.Location).Equals(fullPath, StringComparison.OrdinalIgnoreCase))
                ?? AssemblyLoadContext.Default.LoadFromAssemblyPath(fullPath);
            return Register(assembly, registry, logger);
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Admin override assembly could not be loaded; platform defaults remain active.");
            return [];
        }
    }

    public static IReadOnlyList<(string Name, Type Component)> Register(Assembly assembly, ComponentRegistry registry, ILogger logger)
    {
        var registrations = new List<(string Name, Type Component)>();
        foreach (var component in assembly.GetExportedTypes().Where(type => !type.IsAbstract && typeof(IComponent).IsAssignableFrom(type)))
        {
            if (typeof(LayoutComponentBase).IsAssignableFrom(component)) continue;
            var name = GetName(component);
            if (name is null) continue;
            registry.Register(name, component);
            registrations.Add((name, component));
            logger.LogInformation("Admin override {Name} registered from {Component}.", name, component.FullName);
        }
        return registrations;
    }

    public static string? GetName(Type component)
    {
        var fullName = component.FullName ?? string.Empty;
        var services = fullName.Split(".Services.", 2, StringSplitOptions.None);
        if (services.Length == 2)
        {
            var parts = services[1].Split('.');
            if (parts.Length == 0) return null;
            var service = Kebab(parts[0]);
            var page = Kebab(PageSuffix().Replace(component.Name, string.Empty));
            return page.Length == 0 || page.Equals(service, StringComparison.OrdinalIgnoreCase) || page.Equals("index", StringComparison.OrdinalIgnoreCase)
                ? service
                : $"{service}/{page}";
        }
        return fullName.Contains(".Pages.", StringComparison.Ordinal) ? Kebab(PageSuffix().Replace(component.Name, string.Empty)) : null;
    }

    private static string Kebab(string value) => KebabBoundary().Replace(value, "$1-$2").ToLowerInvariant();
    [GeneratedRegex("Page$")]
    private static partial Regex PageSuffix();
    [GeneratedRegex("([a-z0-9])([A-Z])")]
    private static partial Regex KebabBoundary();
}
