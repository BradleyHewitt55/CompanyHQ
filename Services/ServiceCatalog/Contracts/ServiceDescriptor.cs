namespace CompanyHQ.ServiceCatalog.Contracts;

/// <summary>Public metadata only. Configuration values and credentials never belong here.</summary>
public sealed record ServiceDescriptor(
    string Key, string Name, string Description, string Details,
    bool Core = false, string Owner = "generic", bool DefaultEnabled = true,
    string[]? Dependencies = null, ServiceProviderDescriptor[]? Providers = null,
    string? DefaultProvider = null, string? MenuPath = null,
    ServiceCapabilityDescriptor[]? Capabilities = null,
    string? ReadPermission = null, string[]? AdditionalReadPermissions = null,
    ServiceConfigurationKey[]? ConfigurationKeys = null, bool ProviderChangeRequiresMigration = false);

public sealed record ServiceProviderDescriptor(string Key, string Name, string Description,
    ServiceConfigurationKey[] ConfigurationKeys);

public sealed record ServiceConfigurationKey(string Key, string Description,
    bool Secret = false, bool Required = true, bool RestartRequired = true,
    bool ReadOnly = false, string? ProtectionReason = null);

public sealed record ServiceCapabilityDescriptor(string Key, string Description, string[] Dependencies);


public sealed record ServiceCapabilityDto(string Key, string Description, bool Available);

public sealed record ServiceCatalogDto(ServiceDescriptor Descriptor, bool Enabled,
    string? SelectedProvider, ServiceStatus Status, string? UnavailableReason,
    bool RestartRequired, bool MenuVisible, ServiceCapabilityDto[] Capabilities);

public sealed record ServiceSelection(bool Enabled, string? SelectedProvider);

/// <summary>Safe navigation only; contains no provider or environment settings.</summary>
public sealed record ServiceNavigationDto(string Key, string Name, string Destination, ServiceStatus Status);

/// <summary>A disabled service is absent; an enabled service without a ready provider is unavailable.</summary>
public sealed class ServiceUnavailableException(string serviceKey, ServiceStatus status) : Exception(
    status == ServiceStatus.Disabled ? $"Service '{serviceKey}' is disabled." : $"Service '{serviceKey}' is not configured.")
{
    public string ServiceKey { get; } = serviceKey;
    public ServiceStatus Status { get; } = status;
}

/// <summary>An enabled operation requires a target feature which is disabled or unconfigured.</summary>
public sealed class ServiceDependencyUnavailableException(string serviceKey, string dependencyKey)
    : Exception($"Service '{serviceKey}' requires unavailable service '{dependencyKey}'.")
{
    public string ServiceKey { get; } = serviceKey;
    public string DependencyKey { get; } = dependencyKey;
}
