using Services.Companies.Contracts.Services;

namespace Services.Scaffolding;

public static class ServiceTreeValidator
{
    private static readonly HashSet<string> RootServices = new(StringComparer.OrdinalIgnoreCase) { "Companies", "Auth", "Postgres", "Sqlite", "Scaffolding", "Sales" };

    public static IReadOnlyList<ServiceNode> Validate(string servicesRoot)
    {
        if (!Directory.Exists(servicesRoot)) return [];
        var nodes = new List<ServiceNode>();
        foreach (var manifestPath in Directory.EnumerateFiles(servicesRoot, "*.json", SearchOption.AllDirectories)
                     .Where(path => !IsBuildPath(path)))
        {
            var directory = Path.GetDirectoryName(manifestPath)!;
            var manifest = ServiceManifest.Parse(File.ReadAllText(manifestPath));
            var relative = Path.GetRelativePath(servicesRoot, directory);
            var parts = relative.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            if (parts.Length == 1 && !RootServices.Contains(parts[0]))
                throw new InvalidOperationException($"Domain service '{manifest.Id}' requires an owner and must be written beneath <Owner>/Services/{parts[0]}.");
            if (parts.Length > 1)
            {
                if (parts.Length < 3 || !parts[^2].Equals("Services", StringComparison.OrdinalIgnoreCase))
                    throw new InvalidOperationException($"Service '{manifest.Id}' is misplaced; expected its owner path to end in Services/{parts[^1]}.");
                var expectedParent = ToId(parts[^3]);
                if (!string.Equals(manifest.Parent, expectedParent, StringComparison.OrdinalIgnoreCase))
                    throw new InvalidOperationException($"Service '{manifest.Id}' must declare parent '{expectedParent}' for expected owner path {parts[^3]}/Services/{parts[^1]}.");
            }
            foreach (var source in Directory.EnumerateFiles(directory, "*.cs", SearchOption.AllDirectories).Where(path => !IsBuildPath(path) && !path.Contains($"{Path.DirectorySeparatorChar}Tests{Path.DirectorySeparatorChar}")))
                if (File.ReadAllText(source).Contains(": BaseModel", StringComparison.Ordinal) && !source.Contains($"{Path.DirectorySeparatorChar}Models{Path.DirectorySeparatorChar}"))
                    throw new InvalidOperationException($"Model '{Path.GetFileName(source)}' must live in {directory}/Models.");
            nodes.Add(new ServiceNode(manifest.Id, manifest.Parent, directory));
        }
        return nodes;
    }

    private static bool IsBuildPath(string path) => path.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}") || path.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}");
    private static string ToId(string name) => System.Text.RegularExpressions.Regex.Replace(name, "([a-z0-9])([A-Z])", "$1-$2").ToLowerInvariant();
}

public sealed record ServiceNode(string Id, string? Parent, string Directory);
