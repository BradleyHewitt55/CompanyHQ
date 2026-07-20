using CompanyHQ.Services.Companies.Services;
using Microsoft.Extensions.Configuration;

namespace UnitTests;

public sealed class ServiceScaffolderTests : IDisposable
{
    private readonly string root = Path.Combine(Path.GetTempPath(), $"companyhq-unit-{Guid.NewGuid():N}");

    private (ServiceScaffolder Scaffolder, ServerPaths Paths, CompanyContext Context) Create()
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(
            new Dictionary<string, string?> { ["CompanyHq:ServerRoot"] = root }).Build();
        var paths = new ServerPaths(configuration);
        var context = new CompanyContext { Current = "Acme" };
        Directory.CreateDirectory(Path.Combine(paths.CompanyDirectory("Acme"), "Services"));
        return (new ServiceScaffolder(paths, context, configuration), paths, context);
    }

    [Fact]
    public async Task ValidatesNamesAndRejectsDuplicates()
    {
        var (scaffolder, _, context) = Create();

        await Assert.ThrowsAsync<ArgumentException>(() => scaffolder.ScaffoldAsync("bad/name"));
        await scaffolder.ScaffoldAsync("Billing");
        await Assert.ThrowsAsync<InvalidOperationException>(() => scaffolder.ScaffoldAsync("Billing"));

        context.Current = null;
        await Assert.ThrowsAsync<InvalidOperationException>(() => scaffolder.ScaffoldAsync("Other"));
    }

    [Fact]
    public async Task WritesTheAuthServiceLayout()
    {
        var (scaffolder, paths, _) = Create();

        await scaffolder.ScaffoldAsync("Billing");

        var service = Path.Combine(paths.CompanyDirectory("Acme"), "Services", "Billing");
        Assert.True(File.Exists(Path.Combine(service, "Billing.csproj")));
        Assert.True(File.Exists(Path.Combine(service, "BillingPlugin.cs")));
        Assert.True(File.Exists(Path.Combine(service, "_Imports.razor")));
        Assert.True(File.Exists(Path.Combine(service, "Api", "BillingController.cs")));
        Assert.True(File.Exists(Path.Combine(service, "Components", "BillingPanel.razor")));
        Assert.True(File.Exists(Path.Combine(service, "Pages", "Index.razor")));
        Assert.True(File.Exists(Path.Combine(service, "Services", "BillingService.cs")));
        Assert.True(File.Exists(Path.Combine(service, "Settings", "service.json")));
        foreach (var folder in new[] { "Dtos", "Layout", "Models" })
        {
            Assert.True(Directory.Exists(Path.Combine(service, folder)), folder);
        }
    }

    [Fact]
    public async Task GeneratedPluginUsesTheServiceNamespace()
    {
        var (scaffolder, paths, _) = Create();

        await scaffolder.ScaffoldAsync("Billing");

        var plugin = await File.ReadAllTextAsync(Path.Combine(
            paths.CompanyDirectory("Acme"), "Services", "Billing", "BillingPlugin.cs"));
        Assert.Contains("namespace CompanyHQ.Services.Billing;", plugin);
        Assert.Contains(": ICompanyHqPlugin", plugin);
    }

    [Fact]
    public async Task WritesBuildPropertiesOncePerCompany()
    {
        var (scaffolder, paths, _) = Create();
        var properties = Path.Combine(
            paths.CompanyDirectory("Acme"), "Services", "Directory.Build.props");

        await scaffolder.ScaffoldAsync("Billing");
        await File.WriteAllTextAsync(properties, "edited");
        await scaffolder.ScaffoldAsync("Invoicing");

        // A second service must not clobber a hand-corrected Abstractions path.
        Assert.Equal("edited", await File.ReadAllTextAsync(properties));
    }

    public void Dispose()
    {
        if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
    }
}
