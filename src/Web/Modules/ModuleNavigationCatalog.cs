using Web.Admin.Services;
using Services.Companies.Contracts.Services;

namespace Web.Modules;

public sealed class ModuleNavigationCatalog : IAdminNavigationCatalog
{
    private readonly ModuleCatalog modules;
    public event EventHandler? Changed;
    public ModuleNavigationCatalog(ModuleCatalog modules, IServiceStateStore states)
    {
        this.modules = modules;
        states.Changed += (_, _) => Changed?.Invoke(this, EventArgs.Empty);
    }
    public IReadOnlyList<AdminNavigationItem> GetItems() => Build(null);

    private IReadOnlyList<AdminNavigationItem> Build(string? parent) => modules.Sort(modules.ChildrenOf(parent)
        .Where(module => !modules.IsHidden(module.Manifest.Id) && modules.IsEffectivelyEnabled(module.Manifest.Id)))
        .Select(module => new AdminNavigationItem(module.Manifest.Name, string.Empty, module.Manifest.Icon,
            (module.Manifest.Menu ?? []).Select(item => new AdminNavigationItem(item.Label, item.Url, module.Manifest.Icon))
                .Concat(Build(module.Manifest.Id)).ToArray()))
        .ToArray();
}
