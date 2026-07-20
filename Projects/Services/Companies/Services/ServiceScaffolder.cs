using Abstractions.Companies;
using Microsoft.Extensions.Configuration;

namespace CompanyHQ.Services.Companies.Services;

public sealed class ServiceScaffolder(
    IServerPaths paths,
    ICompanyContext companyContext,
    IConfiguration configuration)
    : IServiceScaffolder
{
    public async Task ScaffoldAsync(
        string serviceName,
        CancellationToken cancellationToken = default)
    {
        NameValidator.Validate(serviceName, nameof(serviceName), "Service");
        var company = companyContext.Current
            ?? throw new InvalidOperationException("A current company is required to scaffold a service.");
        var servicesRoot = Path.Combine(paths.CompanyDirectory(company), "Services");
        var directory = Path.Combine(servicesRoot, serviceName);
        if (Directory.Exists(directory))
        {
            throw new InvalidOperationException($"Service '{serviceName}' already exists.");
        }

        foreach (var folder in ServiceTemplate.EmptyDirectories)
        {
            Directory.CreateDirectory(Path.Combine(directory, folder));
        }

        await WriteAsync(directory, $"{serviceName}.csproj", ServiceTemplate.Project(serviceName), cancellationToken);
        await WriteAsync(directory, $"{serviceName}Plugin.cs", ServiceTemplate.Plugin(serviceName), cancellationToken);
        await WriteAsync(directory, "_Imports.razor", ServiceTemplate.Imports(serviceName), cancellationToken);
        await WriteAsync(directory, "README.md", ServiceTemplate.Readme(serviceName), cancellationToken);
        await WriteAsync(directory, Path.Combine("Api", $"{serviceName}Controller.cs"),
            ServiceTemplate.Controller(serviceName), cancellationToken);
        await WriteAsync(directory, Path.Combine("Components", $"{serviceName}Panel.razor"),
            ServiceTemplate.Panel(serviceName), cancellationToken);
        await WriteAsync(directory, Path.Combine("Pages", "Index.razor"),
            ServiceTemplate.Page(serviceName), cancellationToken);
        await WriteAsync(directory, Path.Combine("Services", $"{serviceName}Service.cs"),
            ServiceTemplate.Service(serviceName), cancellationToken);
        await WriteAsync(directory, Path.Combine("Settings", "service.json"),
            ServiceTemplate.Settings(serviceName), cancellationToken);

        await EnsureBuildPropertiesAsync(servicesRoot, cancellationToken);
    }

    private static async Task WriteAsync(
        string directory,
        string relativePath,
        string content,
        CancellationToken cancellationToken)
    {
        var path = Path.Combine(directory, relativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        await File.WriteAllTextAsync(path, content, cancellationToken);
    }

    // Written once per company rather than per service, so the Abstractions location is
    // corrected in a single place after the platform moves.
    private async Task EnsureBuildPropertiesAsync(
        string servicesRoot,
        CancellationToken cancellationToken)
    {
        var path = Path.Combine(servicesRoot, "Directory.Build.props");
        if (File.Exists(path))
        {
            return;
        }

        var abstractions = configuration["CompanyHq:AbstractionsPath"]
            ?? ServiceTemplate.DefaultAbstractionsPath;
        await File.WriteAllTextAsync(
            path, ServiceTemplate.BuildProperties(abstractions), cancellationToken);
    }
}
