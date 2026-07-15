using Web.Admin.Services;
using Services.Companies.Contracts.Services;

namespace Web.Modules;

public sealed class ModuleServiceCatalog : IServiceCatalog
{
    private readonly ModuleCatalog modules;
    private readonly IServiceStateStore states;
    public event EventHandler? Changed;
    public ModuleServiceCatalog(ModuleCatalog modules, IServiceStateStore states)
    {
        this.modules = modules;
        this.states = states;
        states.Changed += (_, _) => Changed?.Invoke(this, EventArgs.Empty);
    }
    public IReadOnlyList<ServiceSummary> GetServices() => Build(null);

    private IReadOnlyList<ServiceSummary> Build(string? parent) => modules.Sort(modules.ChildrenOf(parent))
        .Where(module => !modules.IsHidden(module.Manifest.Id))
        .Select(module => new ServiceSummary(module.Manifest.Id, module.Manifest.Name, module.Manifest.Description,
            module.Manifest.Version, module.Manifest.Author, module.Manifest.Icon, module.Manifest.Color,
            modules.IsEffectivelyEnabled(module.Manifest.Id), module.SettingsPages.Count > 0, module.Manifest.Parent, Build(module.Manifest.Id)))
        .ToArray();

    public void SetEnabled(string id, bool enabled) => states.SetEnabled(id, enabled).GetAwaiter().GetResult();
}
