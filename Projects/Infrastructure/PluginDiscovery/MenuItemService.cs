namespace CompanyHQ.Infrastructure.PluginDiscovery;

using System.Text;
using System.Text.Json;
using Abstractions.Plugins;
using Abstractions.Services;
using Microsoft.Extensions.Configuration;

/// <summary>
/// Resolves and aggregates menu items from all services (built-in + plugins).
/// Provides hierarchical navigation structure by querying plugin metadata.
/// </summary>
public sealed class MenuItemService : IServiceCatalog
{
    private readonly IPluginCatalog _pluginCatalog;
    private readonly IConfiguration _configuration;
    private IReadOnlyList<ServiceMetadata> _serviceMetadata;

    public MenuItemService(IPluginCatalog pluginCatalog, IConfiguration configuration)
    {
        _pluginCatalog = pluginCatalog;
        _configuration = configuration;
        // Build service metadata from available plugins + built-ins
        _serviceMetadata = BuildServiceMetadata();
    }

    public IReadOnlyList<ServiceMetadata> GetServices(ServiceCategory category = ServiceCategory.All)
    {
        return category switch
        {
            ServiceCategory.BuiltIn => _serviceMetadata.Where(s => s.IsBuiltIn).ToList().AsReadOnly(),
            ServiceCategory.Company => _serviceMetadata.Where(s => !s.IsBuiltIn).ToList().AsReadOnly(),
            ServiceCategory.EnabledOnly => _serviceMetadata.Where(s => s.IsEnabled || s.IsBuiltIn).ToList().AsReadOnly(),
            _ => _serviceMetadata
        };
    }

    public IReadOnlyList<ServiceMenuItem>? GetServiceMenuItems(string serviceId)
    {
        return _serviceMetadata.FirstOrDefault(s => s.Id.Equals(serviceId, StringComparison.OrdinalIgnoreCase))?.MenuItems;
    }

    public IReadOnlyList<ServiceMenuItem> GetNavMenuItems()
    {
        // Return all menu items from enabled services, hierarchical
        var items = new List<ServiceMenuItem>();
        foreach (var service in GetServices(ServiceCategory.EnabledOnly))
        {
            if (service.MenuItems is not null)
            {
                items.AddRange(service.MenuItems);
            }
        }
        return items.AsReadOnly();
    }

    public ServiceMetadata? GetService(string serviceId)
    {
        return _serviceMetadata.FirstOrDefault(s => s.Id.Equals(serviceId, StringComparison.OrdinalIgnoreCase));
    }

    public bool IsServiceEnabled(string serviceId)
    {
        var service = GetService(serviceId);
        return service?.IsEnabled ?? false;
    }

    public void SetServiceEnabled(string serviceId, bool enabled)
    {
        var service = GetService(serviceId);
        if (service is null)
        {
            throw new InvalidOperationException($"Service '{serviceId}' not found.");
        }

        if (service.IsBuiltIn)
        {
            throw new InvalidOperationException($"Built-in service '{serviceId}' cannot be disabled.");
        }

        // Persist enabled state to service.json before updating in-memory metadata
        PersistServiceEnabled(serviceId, enabled);

        // Update the metadata record with new IsEnabled state
        var updated = service with { IsEnabled = enabled };
        var list = _serviceMetadata.ToList();
        var index = list.FindIndex(s => s.Id.Equals(serviceId, StringComparison.OrdinalIgnoreCase));
        if (index >= 0)
        {
            list[index] = updated;
            _serviceMetadata = list.AsReadOnly();
        }
    }

    private void PersistServiceEnabled(string serviceId, bool enabled)
    {
        // Find the company plugin with this service ID to get its assembly path
        var plugin = _pluginCatalog.GetPlugins()
            .FirstOrDefault(p => p.Descriptor.Id.Equals(serviceId, StringComparison.OrdinalIgnoreCase));

        if (plugin is null)
        {
            throw new InvalidOperationException($"Plugin for service '{serviceId}' not found.");
        }

        // Extract service directory name from assembly path.
        // This depends on ServiceTemplate scaffolding pattern: {root}/Companies/{company}/Services/{serviceName}/bin/.../{serviceName}.dll
        // If the directory structure changes, this extraction logic must be updated.
        var assemblyPath = Path.GetFullPath(plugin.AssemblyPath);
        var directories = assemblyPath.Split(Path.DirectorySeparatorChar);
        var servicesIndex = Array.IndexOf(directories, "Services");
        if (servicesIndex < 0 || servicesIndex + 1 >= directories.Length)
        {
            throw new InvalidOperationException($"Cannot extract service name from assembly path: {assemblyPath}");
        }

        var serviceName = directories[servicesIndex + 1];
        var serverRoot = _configuration["CompanyHq:ServerRoot"]
            ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "CompanyHQ");
        var company = _configuration["CompanyHq:Company"]
            ?? throw new InvalidOperationException("CompanyHq:Company configuration is not set.");

        var settingsPath = Path.Combine(
            Path.GetFullPath(serverRoot),
            "Companies",
            company,
            "Services",
            serviceName,
            "Settings",
            "service.json");

        // Ensure the Settings directory exists
        Directory.CreateDirectory(Path.GetDirectoryName(settingsPath)!);

        // Read existing service.json or create empty object, then update "enabled" property
        var jsonText = File.Exists(settingsPath) ? File.ReadAllText(settingsPath, Encoding.UTF8) : "{}";
        var jsonObject = JsonSerializer.Deserialize<Dictionary<string, object>>(jsonText)
            ?? new Dictionary<string, object>();
        jsonObject["enabled"] = enabled;

        var options = new JsonSerializerOptions { WriteIndented = true };
        var json = JsonSerializer.Serialize(jsonObject, options);

        // Write atomically using temp file to prevent corruption on concurrent writes
        var tempPath = settingsPath + ".tmp";
        try
        {
            File.WriteAllText(tempPath, json, Encoding.UTF8);
            File.Move(tempPath, settingsPath, overwrite: true);
        }
        catch
        {
            if (File.Exists(tempPath))
                File.Delete(tempPath);
            throw;
        }
    }

    public void Refresh()
    {
        _serviceMetadata = BuildServiceMetadata();
    }

    private bool ReadServiceEnabled(string pluginId, string serverRoot, string company)
    {
        try
        {
            var parts = pluginId.Split('.');
            if (parts.Length < 3)
                return false; // built-in, default to disabled

            var serviceName = parts[^1];
            var settingsPath = Path.Combine(
                serverRoot,
                "Companies",
                company,
                "Services",
                serviceName,
                "Settings",
                "service.json");

            if (!File.Exists(settingsPath))
                return false;

            using var document = JsonDocument.Parse(File.ReadAllText(settingsPath, Encoding.UTF8));
            return document.RootElement.TryGetProperty("enabled", out var enabled) && enabled.GetBoolean();
        }
        catch
        {
            return false; // Default to disabled if parsing fails
        }
    }

    private IReadOnlyList<ServiceMetadata> BuildServiceMetadata()
    {
        var metadata = new List<ServiceMetadata>();
        var serverRoot = _configuration["CompanyHq:ServerRoot"]
            ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "CompanyHQ");
        var company = _configuration["CompanyHq:Company"]
            ?? throw new InvalidOperationException("CompanyHq:Company configuration is not set.");

        // Built-in services with default menu items
        metadata.Add(new ServiceMetadata(
            Id: "companyhq.dashboard",
            Name: "Dashboard",
            Description: "Admin dashboard.",
            Version: new Version(1, 0, 0),
            IsBuiltIn: true,
            IsToggleable: false,
            MenuItems: new[]
            {
                new ServiceMenuItem(Label: "Dashboard", Path: "/", Icon: "dashboard")
            },
            IsEnabled: true
        ));

        // Scan company services from plugin catalog
        foreach (var plugin in _pluginCatalog.GetPlugins())
        {
            metadata.Add(new ServiceMetadata(
                Id: plugin.Descriptor.Id,
                Name: plugin.Descriptor.Name,
                Description: plugin.Descriptor.Description,
                Version: plugin.Descriptor.Version,
                IsBuiltIn: false,
                IsToggleable: plugin.Descriptor.Activation != PluginActivation.Required,
                MenuItems: null, // Plugins will populate this in a later phase
                IsEnabled: ReadServiceEnabled(plugin.Descriptor.Id, serverRoot, company)
            ));
        }

        return metadata.AsReadOnly();
    }
}
