using System.Diagnostics;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Configuration;

namespace Services.Companies.Data;

public interface ICompanyDeploymentManager
{
    Task DeleteDeployment(string slug, CancellationToken cancellationToken = default);
}

public sealed class CompanyDeploymentManager(IConfiguration configuration) : ICompanyDeploymentManager
{
    private static readonly Regex SlugPattern = new("^[a-z0-9]+(?:-[a-z0-9]+)*$", RegexOptions.CultureInvariant);

    public static bool IsValidSlug(string? slug) => !string.IsNullOrWhiteSpace(slug) && SlugPattern.IsMatch(slug);

    public async Task DeleteDeployment(string slug, CancellationToken cancellationToken = default)
    {
        if (!IsValidSlug(slug)) throw new InvalidOperationException("Company slug is invalid.");
        var companiesRoot = Path.GetFullPath(configuration["Companies:Root"] ?? Path.Combine(Directory.GetCurrentDirectory(), "CompanyHQ", "Companies"));
        var directory = Path.GetFullPath(Path.Combine(companiesRoot, slug));
        if (!directory.StartsWith(companiesRoot + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Company deployment path is invalid.");
        if (!Directory.Exists(directory)) return;
        if ((File.GetAttributes(directory) & FileAttributes.ReparsePoint) != 0)
            throw new InvalidOperationException("Company deployment directory cannot be a link.");

        if (File.Exists(Path.Combine(directory, "docker-compose.yml")))
            await RunDockerCompose(directory, cancellationToken);
        Directory.Delete(directory, recursive: true);
    }

    private static async Task RunDockerCompose(string directory, CancellationToken cancellationToken)
    {
        using var process = new Process { StartInfo = new ProcessStartInfo("docker") { WorkingDirectory = directory, UseShellExecute = false } };
        process.StartInfo.ArgumentList.Add("compose");
        process.StartInfo.ArgumentList.Add("down");
        process.StartInfo.ArgumentList.Add("--volumes");
        process.StartInfo.ArgumentList.Add("--remove-orphans");
        process.Start();
        await process.WaitForExitAsync(cancellationToken);
        if (process.ExitCode != 0) throw new InvalidOperationException("Could not stop the company deployment; nothing was deleted.");
    }
}
