namespace Services.Companies.Contracts;

public sealed class ComponentRegistry(Type fallbackComponent)
{
    private readonly object _gate = new();
    private readonly Dictionary<string, List<Registration>> _components = new(StringComparer.OrdinalIgnoreCase);

    public Type FallbackComponent { get; } = fallbackComponent ?? throw new ArgumentNullException(nameof(fallbackComponent));

    public void Register(string name, Type component, Func<bool>? isEnabled = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(component);
        lock (_gate)
        {
            var key = Normalize(name);
            if (!_components.TryGetValue(key, out var registrations))
            {
                registrations = [];
                _components[key] = registrations;
            }
            registrations.Add(new Registration(component, isEnabled ?? (() => true)));
        }
    }

    public Type Resolve(string? name)
    {
        lock (_gate)
        {
            if (name is null || !_components.TryGetValue(Normalize(name), out var registrations)) return FallbackComponent;
            return registrations.LastOrDefault(registration => registration.IsEnabled())?.Component ?? FallbackComponent;
        }
    }

    public bool Unregister(string name, Type component)
    {
        lock (_gate)
        {
            var key = Normalize(name);
            if (!_components.TryGetValue(key, out var registrations)) return false;
            var removed = registrations.RemoveAll(registration => registration.Component == component) > 0;
            if (registrations.Count == 0) _components.Remove(key);
            return removed;
        }
    }

    public IReadOnlyDictionary<string, Type> Snapshot()
    {
        lock (_gate)
        {
            return _components
                .Select(pair => (pair.Key, Registration: pair.Value.LastOrDefault(registration => registration.IsEnabled())))
                .Where(pair => pair.Registration is not null)
                .ToDictionary(pair => pair.Key, pair => pair.Registration!.Component, StringComparer.OrdinalIgnoreCase);
        }
    }

    private static string Normalize(string name) => name.Trim().Trim('/').ToLowerInvariant();
    private sealed record Registration(Type Component, Func<bool> IsEnabled);
}
