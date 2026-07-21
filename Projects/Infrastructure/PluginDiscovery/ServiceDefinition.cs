namespace CompanyHQ.Infrastructure.PluginDiscovery;

using Abstractions.Services;

/// <summary>
/// Base class for services to expose their metadata.
/// Plugins and built-in services inherit from this to declare their menu items and capabilities.
/// </summary>
public abstract class ServiceDefinition
{
    public abstract ServiceMetadata Metadata { get; }
}
