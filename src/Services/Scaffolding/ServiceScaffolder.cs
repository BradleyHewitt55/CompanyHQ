using System.Text.Json;
using System.Text.RegularExpressions;

namespace Services.Scaffolding;

public sealed record ServiceTemplate(string Id, string Name, string Description);
public sealed record ScaffoldServiceRequest(string Name, string Parent);

public static class ServiceScaffolder
{
    public static IReadOnlyList<ServiceTemplate> Catalog { get; } =
    [
        new("blank", "Blank service", "A service with API, models, pages, settings, child services, and tests."),
        new("commerce", "Commerce service", "A service suitable for sales and commerce capabilities.")
    ];

    public static bool IsValidName(string name) => !string.IsNullOrWhiteSpace(name) && char.IsUpper(name[0]) && name.All(char.IsLetterOrDigit);

    public static string Generate(string outputRoot, string name, string? parent)
    {
        if (!IsValidName(name)) throw new ArgumentException("Service name must be PascalCase and contain only letters and numbers.", nameof(name));
        if (string.IsNullOrWhiteSpace(parent)) throw new ArgumentException("A parent/owner service is required for domain services.", nameof(parent));
        var parentDirectory = FindParent(outputRoot, parent) ?? throw new DirectoryNotFoundException($"Parent service '{parent}' was not found beneath {Path.Combine(outputRoot, "src", "Services")}.");
        var directory = Path.Combine(parentDirectory, "Services", name);
        if (Directory.Exists(directory)) throw new InvalidOperationException($"Service directory already exists: {directory}");
        foreach (var folder in new[] { "Api", "Models", "Pages", "Settings", "Services", "Tests" }) Directory.CreateDirectory(Path.Combine(directory, folder));
        var id = ToKebab(name);
        File.WriteAllText(Path.Combine(directory, $"{id}.json"), JsonSerializer.Serialize(new
        {
            id, name, version = "1.0.0", author = Environment.UserName, icon = "extension", color = "#6366f1",
            tags = Array.Empty<string>(), description = $"{name} service.", hidden = false, parent = ToKebab(parent),
            menu = new[] { new { label = name, url = $"/admin/services/{id}/index" } }
        }, new JsonSerializerOptions { WriteIndented = true }));
        var companies = FindCompaniesProject(outputRoot);
        var reference = companies is null
            ? $"<Reference Include=\"Companies\"><HintPath>{Path.GetRelativePath(directory, File.Exists(Path.Combine(outputRoot, "lib", "Companies.dll")) ? Path.Combine(outputRoot, "lib", "Companies.dll") : typeof(Services.Companies.Contracts.Services.IServiceModule).Assembly.Location)}</HintPath></Reference>"
            : $"<ProjectReference Include=\"{Path.GetRelativePath(directory, companies)}\" />";
        File.WriteAllText(Path.Combine(directory, $"{name}.csproj"), $$"""
            <Project Sdk="Microsoft.NET.Sdk.Razor">
              <PropertyGroup><TargetFramework>net10.0</TargetFramework><Nullable>enable</Nullable><ImplicitUsings>enable</ImplicitUsings><AssemblyName>{{name}}</AssemblyName><RootNamespace>Services.{{name}}</RootNamespace></PropertyGroup>
              <ItemGroup><FrameworkReference Include="Microsoft.AspNetCore.App" />{{reference}}</ItemGroup>
              <ItemGroup><Content Update="{{id}}.json" CopyToOutputDirectory="PreserveNewest" /><EmbeddedResource Include="{{id}}.json" LogicalName="service-manifest.json" /></ItemGroup>
              <ItemGroup><Compile Remove="Tests\**\*.cs" /></ItemGroup>
            </Project>
            """);
        File.WriteAllText(Path.Combine(directory, $"{name}Module.cs"), $$"""
            using Services.Companies.Contracts.Services;
            using Microsoft.AspNetCore.Builder;
            using Microsoft.AspNetCore.Http;
            using Microsoft.AspNetCore.Routing;
            using Microsoft.Extensions.DependencyInjection;
            namespace Services.{{name}};
            public sealed class {{name}}Module : IServiceModule
            {
                public ServiceManifest Manifest { get; } = ServiceManifest.Load(typeof({{name}}Module).Assembly);
                public IReadOnlyList<ServiceComponentRegistration> Components => [new("index", typeof(Pages.IndexPage))];
                public void Configure(IServiceCollection services) { }
                public void MapEndpoints(IEndpointRouteBuilder endpoints) => endpoints.MapGet("/api/services/{{id}}", () => Results.Ok(new { service = "{{id}}" }));
            }
            """);
        File.WriteAllText(Path.Combine(directory, "Pages", "IndexPage.razor"), $"<section data-page=\"{id}\"><h1>{name}</h1></section>{Environment.NewLine}");
        File.WriteAllText(Path.Combine(directory, "README.md"), $"# {name}{Environment.NewLine}{Environment.NewLine}Child service owned by {parent}.{Environment.NewLine}");
        WriteStarterTest(directory, name);
        var servicesRoot = Path.Combine(outputRoot, "src", "Services");
        if (Directory.Exists(servicesRoot)) ServiceTreeValidator.Validate(servicesRoot);
        return directory;
    }

    private static void WriteStarterTest(string directory, string name)
    {
        File.WriteAllText(Path.Combine(directory, "Tests", $"{name}.Tests.csproj"), $$"""
            <Project Sdk="Microsoft.NET.Sdk"><PropertyGroup><TargetFramework>net10.0</TargetFramework><Nullable>enable</Nullable><ImplicitUsings>enable</ImplicitUsings><IsPackable>false</IsPackable></PropertyGroup><ItemGroup><PackageReference Include="Microsoft.NET.Test.Sdk" /><PackageReference Include="xunit" /><PackageReference Include="xunit.runner.visualstudio" /></ItemGroup><ItemGroup><Using Include="Xunit" /></ItemGroup><ItemGroup><ProjectReference Include="..\{{name}}.csproj" /></ItemGroup></Project>
            """);
        File.WriteAllText(Path.Combine(directory, "Tests", "StarterTests.cs"), $"namespace Services.{name}.Tests; public sealed class StarterTests {{ [Fact] public void ServiceLoads() => Assert.NotNull(typeof({name}Module)); }}{Environment.NewLine}");
    }

    private static string? FindParent(string root, string parent)
    {
        if (parent.Equals("Admin", StringComparison.OrdinalIgnoreCase))
        {
            var admin = Path.Combine(root, "CompanyHQ", "Admin");
            Directory.CreateDirectory(Path.Combine(admin, "Services"));
            return admin;
        }
        return Directory.Exists(Path.Combine(root, "src", "Services"))
        ? Directory.EnumerateFiles(Path.Combine(root, "src", "Services"), "*.json", SearchOption.AllDirectories)
            .Where(path => !path.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}") && !path.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}"))
            .Select(Path.GetDirectoryName).FirstOrDefault(directory => directory is not null && Path.GetFileName(directory).Equals(parent, StringComparison.OrdinalIgnoreCase))
        : null;
    }
    private static string? FindCompaniesProject(string root) => Directory.EnumerateFiles(root, "Companies.csproj", SearchOption.AllDirectories).FirstOrDefault();
    private static string ToKebab(string value) => Regex.Replace(value, "([a-z0-9])([A-Z])", "$1-$2").ToLowerInvariant();
}
