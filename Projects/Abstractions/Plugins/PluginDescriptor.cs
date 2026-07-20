namespace Abstractions.Plugins;

public sealed record PluginDescriptor(
    string Id,
    string Name,
    string Description,
    Version Version,
    PluginOrigin Origin,
    PluginActivation Activation,
    IReadOnlyList<string> Dependencies);
