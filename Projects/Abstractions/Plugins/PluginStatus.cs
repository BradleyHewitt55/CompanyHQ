namespace Abstractions.Plugins;

public sealed record PluginStatus(
    PluginDescriptor Descriptor,
    bool Enabled,
    string? Company,
    string AssemblyPath);
