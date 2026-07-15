namespace Services.Companies.Contracts;

public sealed class LayoutRegistry
{
    private readonly object gate = new();
    private readonly Dictionary<string, List<Type>> layouts = new(StringComparer.OrdinalIgnoreCase);

    public void Register(string name, Type layout)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(layout);
        lock (gate)
        {
            if (!layouts.TryGetValue(name.Trim(), out var registrations))
                layouts[name.Trim()] = registrations = [];
            registrations.Add(layout);
        }
    }

    public Type Resolve(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        lock (gate)
        {
            if (!layouts.TryGetValue(name.Trim(), out var registrations) || registrations.Count == 0)
                throw new KeyNotFoundException($"Unknown layout '{name}'.");
            return registrations[^1];
        }
    }

    public bool Unregister(string name, Type layout)
    {
        lock (gate)
        {
            if (!layouts.TryGetValue(name.Trim(), out var registrations)) return false;
            var index = registrations.FindLastIndex(item => item == layout);
            if (index < 0) return false;
            registrations.RemoveAt(index);
            if (registrations.Count == 0) layouts.Remove(name.Trim());
            return true;
        }
    }
}
