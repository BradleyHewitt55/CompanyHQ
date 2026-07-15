using FluentAssertions;

namespace Services.Companies.Tests;

public sealed class ReadmeTests
{
    [Fact]
    public void EveryProjectAndServiceHasReadme()
    {
        var root = FindRoot();
        foreach (var project in new[] { "Web", "Cli" })
            File.ReadAllText(Path.Combine(root, "src", project, "README.md")).Should().NotBeNullOrWhiteSpace();
        foreach (var manifest in Directory.EnumerateFiles(Path.Combine(root, "src", "Services"), "*.json", SearchOption.AllDirectories).Where(path => !path.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}") && !path.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}")))
            File.ReadAllText(Path.Combine(Path.GetDirectoryName(manifest)!, "README.md")).Should().NotBeNullOrWhiteSpace();
    }

    private static string FindRoot()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
            if (File.Exists(Path.Combine(directory.FullName, "CompanyHQ.sln"))) return directory.FullName;
        throw new DirectoryNotFoundException();
    }
}
