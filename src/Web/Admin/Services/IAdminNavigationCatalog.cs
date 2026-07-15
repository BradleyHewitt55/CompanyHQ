namespace Web.Admin.Services;

public interface IAdminNavigationCatalog
{
    event EventHandler? Changed;
    IReadOnlyList<AdminNavigationItem> GetItems();
}

public sealed record AdminNavigationItem(string Label, string Path, string Icon, IReadOnlyList<AdminNavigationItem>? Children = null);
internal sealed class EmptyAdminNavigationCatalog : IAdminNavigationCatalog
{
    public event EventHandler? Changed { add { } remove { } }
    public IReadOnlyList<AdminNavigationItem> GetItems() => [];
}
