using System.Reflection;
using CompanyHQ.ServiceCatalog.Contracts;

namespace CompanyHQ.ServiceCatalog;

/// <summary>Runtime provider readiness describes loaded configuration, never a stored credential.</summary>
public sealed record ServiceRegistration(ServiceDescriptor Descriptor, Assembly Assembly,
    string? LoadedProvider = null, Func<IServiceProvider, bool>? IsConfigured = null,
    Func<IServiceProvider, string, CancellationToken, Task<bool>>? IsCapabilityConfigured = null);

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
public sealed class ServiceOwnerAttribute(string key) : Attribute
{
    public string Key { get; } = key;
}
