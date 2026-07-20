using CompanyHQ.Services.Companies.Services;
using Microsoft.Extensions.Configuration;

namespace UnitTests;

public sealed class ServiceCatalogTests : IDisposable
{
    private readonly string root = Path.Combine(Path.GetTempPath(), $"companyhq-catalog-{Guid.NewGuid():N}");
    private readonly ServerPaths paths;
    private readonly CompanyContext context = new();

    public ServiceCatalogTests()
    {
        paths = new ServerPaths(new ConfigurationBuilder().AddInMemoryCollection(
            new Dictionary<string, string?> { ["CompanyHq:ServerRoot"] = root }).Build());
    }

    [Fact]
    public async Task ListsServicesOfTheCurrentCompanyInOrder()
    {
        var services = Path.Combine(paths.CompanyDirectory("Acme"), "Services");
        Directory.CreateDirectory(Path.Combine(services, "Invoicing"));
        Directory.CreateDirectory(Path.Combine(services, "Billing"));
        context.Current = "Acme";

        Assert.Equal(["Billing", "Invoicing"], await new ServiceCatalog(paths, context).ListAsync());
    }

    [Fact]
    public async Task ReturnsEmptyWhenNoCompanyIsSelected()
    {
        Assert.Empty(await new ServiceCatalog(paths, context).ListAsync());
    }

    [Fact]
    public async Task DoesNotLeakAnotherCompanysServices()
    {
        Directory.CreateDirectory(Path.Combine(paths.CompanyDirectory("Acme"), "Services", "Billing"));
        Directory.CreateDirectory(Path.Combine(paths.CompanyDirectory("Globex"), "Services"));
        context.Current = "Globex";

        Assert.Empty(await new ServiceCatalog(paths, context).ListAsync());
    }

    public void Dispose()
    {
        if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
    }
}
