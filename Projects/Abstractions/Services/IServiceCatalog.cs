namespace Abstractions.Services;

/// <summary>
/// Service discovery and metadata provider.
/// Retrieves service metadata and menu items from built-in and plugin-contributed services.
/// </summary>
public interface IServiceCatalog
{
    /// <summary>
    /// Retrieves service metadata (built-in + company services, filtered by category)
    /// </summary>
    IReadOnlyList<ServiceMetadata> GetServices(ServiceCategory category = ServiceCategory.All);

    /// <summary>
    /// Gets menu items for a specific service by id
    /// </summary>
    IReadOnlyList<ServiceMenuItem>? GetServiceMenuItems(string serviceId);

    /// <summary>
    /// Merged menu items: built-in + company services, hierarchical
    /// </summary>
    IReadOnlyList<ServiceMenuItem> GetNavMenuItems();

    /// <summary>
    /// Gets a specific service by id, or null if not found
    /// </summary>
    ServiceMetadata? GetService(string serviceId);

    /// <summary>
    /// Checks if a service is currently enabled
    /// </summary>
    bool IsServiceEnabled(string serviceId);

    /// <summary>
    /// Updates the enabled state of a service (company services only; built-ins cannot be disabled)
    /// </summary>
    void SetServiceEnabled(string serviceId, bool enabled);

    /// <summary>
    /// Refreshes the catalog from disk (reads service.json files and plugin assemblies)
    /// </summary>
    void Refresh();
}

public enum ServiceCategory
{
    All,
    BuiltIn,
    Company,
    EnabledOnly
}
