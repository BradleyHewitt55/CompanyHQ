using System.Reflection;
using System.Runtime.Loader;
using Services.Companies.Contracts;
using Services.Companies.Contracts.Services;
using Microsoft.Extensions.DependencyInjection;

namespace Web.Modules;

public sealed class ModuleCatalog
{
    private readonly List<IServiceModule> modules = [];
    public IReadOnlyList<IServiceModule> Modules => modules;
    public IServiceStateStore? StateStore { get; set; }

    public static ModuleCatalog Load(string path, IServiceCollection services, ComponentRegistry registry, ILogger logger) =>
        Load(path, services, registry, new LayoutRegistry(), logger);

    public static ModuleCatalog Load(string path, IServiceCollection services, ComponentRegistry registry, LayoutRegistry layouts, ILogger logger)
    {
        var catalog = new ModuleCatalog();
        var paths = path.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Where(Directory.Exists).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        if (paths.Length == 0) return catalog;
        AssemblyLoadContext.Default.Resolving += (_, name) =>
        {
            var dependency = paths.Append(AppContext.BaseDirectory).Select(folder => Path.Combine(folder, name.Name + ".dll")).FirstOrDefault(File.Exists);
            return dependency is not null ? AssemblyLoadContext.Default.LoadFromAssemblyPath(Path.GetFullPath(dependency)) : null;
        };

        var discovered = new List<(IServiceModule Module, Assembly Assembly)>();
        foreach (var dll in paths.SelectMany(folder => Directory.EnumerateFiles(folder, "*.dll"))
            .Where(dll => Directory.EnumerateFiles(Path.GetDirectoryName(dll)!, "*.json")
                .Any(json => Path.GetFileNameWithoutExtension(json).Equals(Path.GetFileNameWithoutExtension(dll), StringComparison.OrdinalIgnoreCase))))
        {
            try
            {
                var assemblyName = AssemblyName.GetAssemblyName(dll);
                var assembly = AssemblyLoadContext.Default.Assemblies.FirstOrDefault(candidate => AssemblyName.ReferenceMatchesDefinition(candidate.GetName(), assemblyName))
                    ?? AssemblyLoadContext.Default.LoadFromAssemblyPath(Path.GetFullPath(dll));
                foreach (var type in assembly.GetTypes().Where(type => !type.IsAbstract && typeof(IServiceModule).IsAssignableFrom(type)))
                {
                    var module = (IServiceModule)Activator.CreateInstance(type)!;
                    if (discovered.All(item => !item.Module.Manifest.Id.Equals(module.Manifest.Id, StringComparison.OrdinalIgnoreCase))) discovered.Add((module, assembly));
                }
            }
            catch (Exception exception) { logger.LogWarning(exception, "Skipping module assembly {ModuleAssembly} because it could not be loaded.", dll); }
        }

        foreach (var item in discovered.OrderBy(item => item.Module.Manifest.Id.Equals("postgres", StringComparison.OrdinalIgnoreCase) ? 0 : item.Module.Manifest.Hidden ? 1 : 2).ThenBy(item => item.Module.Manifest.Order ?? int.MaxValue))
        {
            try
            {
                item.Module.Configure(services);
                catalog.modules.Add(item.Module);
                var entityTypes = services.LastOrDefault(descriptor => descriptor.ServiceType == typeof(EntityTypeRegistry))?.ImplementationInstance as EntityTypeRegistry;
                entityTypes?.AddFromAssembly(item.Assembly);
                foreach (var component in item.Module.Components)
                {
                    var id = item.Module.Manifest.Id;
                    var name = item.Module.Manifest.Hidden || component.Name.Equals(id, StringComparison.OrdinalIgnoreCase) ? component.Name : $"services/{id}/{component.Name}";
                    registry.Register(name, component.ComponentType, () => item.Module.Manifest.Hidden || catalog.IsEffectivelyEnabled(id));
                }
                RegisterSettingsPages(item.Module, registry, catalog);
            }
            catch (Exception exception) { logger.LogWarning(exception, "Skipping service {ServiceId} because configuration failed.", item.Module.Manifest.Id); }
        }
        return catalog;
    }

    public void MapEndpoints(IEndpointRouteBuilder endpoints)
    {
        foreach (var module in modules) module.MapEndpoints(endpoints);
    }

    public bool AddRuntime(IServiceModule module, Assembly assembly, ComponentRegistry registry, ILogger logger)
    {
        lock (modules)
        {
            if (Find(module.Manifest.Id) is not null) return false;
            try
            {
                modules.Add(module);
                foreach (var component in module.Components)
                {
                    var id = module.Manifest.Id;
                    var name = component.Name.Equals(id, StringComparison.OrdinalIgnoreCase) ? component.Name : $"services/{id}/{component.Name}";
                    registry.Register(name, component.ComponentType, () => IsEffectivelyEnabled(id));
                }
                RegisterSettingsPages(module, registry, this);
                return true;
            }
            catch (Exception exception)
            {
                logger.LogError(exception, "Runtime service {ServiceId} could not be registered.", module.Manifest.Id);
                modules.Remove(module);
                return false;
            }
        }
    }

    public string? MatchService(PathString path) => modules.FirstOrDefault(module => path.StartsWithSegments($"/api/services/{module.Manifest.Id}", StringComparison.OrdinalIgnoreCase))?.Manifest.Id;
    public bool TryMatchSettingsRoute(PathString path, out IServiceModule? module, out ServiceSettingsPage? page)
    {
        module = null;
        page = null;
        var segments = path.Value?.Trim('/').Split('/', StringSplitOptions.RemoveEmptyEntries) ?? [];
        if (segments.Length < 4 || !segments[0].Equals("admin", StringComparison.OrdinalIgnoreCase) || !segments[1].Equals("services", StringComparison.OrdinalIgnoreCase) || !segments[3].Equals("settings", StringComparison.OrdinalIgnoreCase))
            return false;

        module = Find(segments[2]);
        if (module is null || IsHidden(module.Manifest.Id)) return true;
        var name = segments.Length == 4 ? null : string.Join('/', segments.Skip(4));
        page = FindSettingsPage(module, name);
        return true;
    }
    public bool IsHidden(string id)
    {
        var module = Find(id);
        return module is not null && (module.Manifest.Hidden || Ancestors(module).Any(parent => parent.Manifest.Hidden));
    }

    public bool IsEffectivelyEnabled(string id)
    {
        var module = Find(id);
        return module is not null && StateStore?.IsEnabled(id) != false && Ancestors(module).All(parent => StateStore?.IsEnabled(parent.Manifest.Id) != false);
    }

    public bool CanEnable(string id) => Find(id) is { } module && Ancestors(module).All(parent => StateStore?.IsEnabled(parent.Manifest.Id) != false);
    public IServiceModule? Find(string id) => modules.FirstOrDefault(module => module.Manifest.Id.Equals(id, StringComparison.OrdinalIgnoreCase));
    public IReadOnlyList<IServiceModule> ChildrenOf(string? parent) => modules.Where(module =>
        string.Equals(module.Manifest.Parent, parent, StringComparison.OrdinalIgnoreCase) ||
        (parent is null && module.Manifest.Parent is not null && Find(module.Manifest.Parent) is null)).ToArray();

    public IOrderedEnumerable<IServiceModule> Sort(IEnumerable<IServiceModule> source) => source
        .OrderBy(module => module.Manifest.Order.HasValue ? 0 : 1)
        .ThenBy(module => module.Manifest.Order ?? int.MaxValue)
        .ThenBy(module => StateStore?.EnrollmentSequence(module.Manifest.Id) ?? long.MaxValue);

    private IEnumerable<IServiceModule> Ancestors(IServiceModule module)
    {
        for (var parent = Find(module.Manifest.Parent ?? string.Empty); parent is not null; parent = Find(parent.Manifest.Parent ?? string.Empty)) yield return parent;
    }

    private static void RegisterSettingsPages(IServiceModule module, ComponentRegistry registry, ModuleCatalog catalog)
    {
        var pages = module.SettingsPages;
        if (pages.Count == 0 || module.Manifest.Hidden) return;
        var id = module.Manifest.Id;
        registry.Register($"services/{id}/settings", pages[0].ComponentType, () => catalog.IsEffectivelyEnabled(id));
        foreach (var page in pages.Where(page => !string.IsNullOrWhiteSpace(page.Name) && !page.Name.Equals("settings", StringComparison.OrdinalIgnoreCase)))
            registry.Register($"services/{id}/settings/{page.Name}", page.ComponentType, () => catalog.IsEffectivelyEnabled(id));
    }

    private static ServiceSettingsPage? FindSettingsPage(IServiceModule module, string? name) =>
        string.IsNullOrWhiteSpace(name)
            ? module.SettingsPages.FirstOrDefault()
            : module.SettingsPages.FirstOrDefault(page => page.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
}
