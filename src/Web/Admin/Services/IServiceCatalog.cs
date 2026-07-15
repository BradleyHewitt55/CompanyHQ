namespace Web.Admin.Services;

public interface IServiceCatalog
{
    event EventHandler? Changed;
    IReadOnlyList<ServiceSummary> GetServices();
    void SetEnabled(string id, bool enabled);
}

public sealed record ServiceSummary(string Id, string Name, string Description, string Version, string Author, string Icon, string Color, bool Enabled, bool HasSettings = false, string? Parent = null, IReadOnlyList<ServiceSummary>? Children = null);

internal sealed class StubServiceCatalog : IServiceCatalog
{
    public event EventHandler? Changed;
    private readonly List<ServiceSummary> _services =
    [
        new("orders", "Orders", "Manage customer orders and fulfilment.", "1.0.0", "CompanyHQ", "shopping_bag", "#a855f7", true),
        new("inventory", "Inventory", "Track products and stock levels.", "1.0.0", "CompanyHQ", "inventory_2", "#22c55e", false),
        new("analytics", "Analytics", "Understand company performance.", "1.0.0", "CompanyHQ", "monitoring", "#38bdf8", true)
    ];

    public IReadOnlyList<ServiceSummary> GetServices() => _services;

    public void SetEnabled(string id, bool enabled)
    {
        var index = _services.FindIndex(service => service.Id.Equals(id, StringComparison.OrdinalIgnoreCase));
        if (index >= 0) _services[index] = _services[index] with { Enabled = enabled };
        Changed?.Invoke(this, EventArgs.Empty);
    }
}
