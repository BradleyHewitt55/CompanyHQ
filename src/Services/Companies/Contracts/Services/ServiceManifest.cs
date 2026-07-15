using System.Text.Json;
using System.Reflection;

namespace Services.Companies.Contracts.Services;

public sealed record ServiceManifest(
    string Id,
    string Name,
    string Version,
    string Author,
    string Icon,
    string Color,
    IReadOnlyList<string> Tags,
    string Description,
    bool Hidden = false,
    int? Order = null,
    IReadOnlyList<ServiceMenuItem>? Menu = null,
    string? Parent = null)
{
    public static ServiceManifest Load(Assembly assembly, string resourceName = "service-manifest.json")
    {
        using var stream = assembly.GetManifestResourceStream(resourceName) ?? throw new ServiceManifestException($"Assembly '{assembly.GetName().Name}' has no embedded {resourceName}.");
        using var reader = new StreamReader(stream);
        return Parse(reader.ReadToEnd());
    }

    public static ServiceManifest Parse(string json)
    {
        var manifest = JsonSerializer.Deserialize<ServiceManifest>(json, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        }) ?? throw new ServiceManifestException("The service manifest is empty or invalid JSON.");

        var missing = new List<string>();
        if (string.IsNullOrWhiteSpace(manifest.Id)) missing.Add("id");
        if (string.IsNullOrWhiteSpace(manifest.Name)) missing.Add("name");
        if (string.IsNullOrWhiteSpace(manifest.Version)) missing.Add("version");
        if (string.IsNullOrWhiteSpace(manifest.Author)) missing.Add("author");
        if (missing.Count > 0) throw new ServiceManifestException($"Missing required manifest field(s): {string.Join(", ", missing)}.");
        var menu = manifest.Menu ?? [];
        var invalid = menu.FirstOrDefault(item => string.IsNullOrWhiteSpace(item.Url));
        if (invalid is not null) throw new ServiceManifestException($"Menu item '{invalid.Label}' is missing required field: url.");
        return manifest with { Tags = manifest.Tags ?? [], Menu = menu };
    }
}

public sealed record ServiceMenuItem(string Label, string Url);

public sealed class ServiceManifestException(string message) : Exception(message);
