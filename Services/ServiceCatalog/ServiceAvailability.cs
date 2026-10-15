using CompanyHQ.ServiceCatalog.Contracts;
using Microsoft.Extensions.DependencyInjection;

namespace CompanyHQ.ServiceCatalog;

internal sealed class ServiceAvailability(ServiceCatalogRegistry registry, IServiceScopeFactory scopes) : IServiceAvailability
{
    public async Task<IReadOnlyList<ServiceCatalogDto>> ListAsync(CancellationToken cancellationToken = default)
    {
        // A fresh scope avoids a request's tracked or uncommitted selections and stale identity maps.
        await using var scope = scopes.CreateAsyncScope();
        var selections = await scope.ServiceProvider.GetRequiredService<IServiceSelectionReader>().ReadAsync(cancellationToken);
        var results = new Dictionary<string, ServiceCatalogDto>(StringComparer.Ordinal);
        ServiceCatalogDto Evaluate(string key, HashSet<string> visiting)
        {
            if (results.TryGetValue(key, out var existing)) return existing;
            var registration = registry.Services[key];
            var descriptor = registration.Descriptor;
            if (!visiting.Add(key)) throw new InvalidOperationException("Catalog dependencies must be acyclic.");
            selections.TryGetValue(key, out var selection);
            var enabled = descriptor.Core || (selection?.Enabled ?? descriptor.DefaultEnabled);
            var provider = selection?.SelectedProvider ?? descriptor.DefaultProvider;
            var restart = provider != registration.LoadedProvider;
            var dependencyUnavailable = (descriptor.Dependencies ?? []).Any(dependency =>
                !registry.Services.ContainsKey(dependency) || Evaluate(dependency, visiting).Status != ServiceStatus.EnabledReady);
            var configured = !restart && !dependencyUnavailable && (registration.IsConfigured?.Invoke(scope.ServiceProvider) ?? true);
            var status = !enabled ? ServiceStatus.Disabled : configured ? ServiceStatus.EnabledReady : ServiceStatus.EnabledNotConfigured;
            visiting.Remove(key);
            return results[key] = new(descriptor, enabled, provider, status,
                !enabled ? "Disabled" : restart ? "Provider change requires configuration and restart" : dependencyUnavailable ? "Required service unavailable" : !configured ? "Not configured" : null,
                restart, enabled && descriptor.MenuPath is not null, []);
        }
        var catalog = registry.Services.Keys.Order(StringComparer.Ordinal).Select(key => Evaluate(key, new(StringComparer.Ordinal))).ToArray();
        for (var index = 0; index < catalog.Length; index++)
        {
            var service = catalog[index];
            var readiness = registry.Services[service.Descriptor.Key].IsCapabilityConfigured;
            var capabilities = new List<ServiceCapabilityDto>();
            // Optional capabilities can point both ways (Sales shipping / Shipping catalog).
            // Resolve them after required dependency statuses, rather than recursively evaluating capabilities.
            foreach (var capability in service.Descriptor.Capabilities ?? [])
            {
                var available = service.Status == ServiceStatus.EnabledReady && capability.Dependencies.All(dependency =>
                    results.TryGetValue(dependency, out var target) && target.Status == ServiceStatus.EnabledReady);
                if (available && readiness is not null)
                    available = await readiness(scope.ServiceProvider, capability.Key, cancellationToken);
                capabilities.Add(new(capability.Key, capability.Description, available));
            }
            catalog[index] = service with { Capabilities = capabilities.ToArray() };
        }
        return catalog;
    }

    public async Task<ServiceCatalogDto?> GetAsync(string key, CancellationToken cancellationToken = default) =>
        (await ListAsync(cancellationToken)).FirstOrDefault(service => service.Descriptor.Key == key);

    public async Task RequireAsync(string key, bool requireReady = false, CancellationToken cancellationToken = default)
    {
        var service = await GetAsync(key, cancellationToken);
        if (service is null || !service.Enabled) throw new ServiceUnavailableException(key, ServiceStatus.Disabled);
        // A disabled required feature prevents operation even when the caller only needs enablement.
        foreach (var dependency in service.Descriptor.Dependencies ?? [])
            await RequireAsync(dependency, requireReady, cancellationToken);
        if (requireReady && service.Status != ServiceStatus.EnabledReady)
            throw new ServiceUnavailableException(key, service.Status);
    }
}

public sealed class ServiceCatalogRegistry
{
    internal ServiceCatalogRegistry(IEnumerable<ServiceRegistration> services) =>
        Services = services.ToDictionary(service => service.Descriptor.Key, StringComparer.Ordinal);
    public IReadOnlyDictionary<string, ServiceRegistration> Services { get; }
}
