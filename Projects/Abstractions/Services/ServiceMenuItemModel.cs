namespace Abstractions.Services;

public record ServiceMenuItem(
    string Label,
    string? Path = null,
    string? Icon = null,
    IReadOnlyList<ServiceMenuItem>? Children = null);

public record ServiceMetadata(
    string Id,
    string Name,
    string Description,
    Version Version,
    bool IsBuiltIn,
    bool IsToggleable,
    IReadOnlyList<ServiceMenuItem>? MenuItems = null,
    bool IsEnabled = true);
