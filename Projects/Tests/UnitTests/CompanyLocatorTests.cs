using CompanyHQ.Services.Companies.Services;

namespace UnitTests;

public sealed class CompanyLocatorTests : IDisposable
{
    private readonly string root = Path.Combine(Path.GetTempPath(), $"companyhq-locate-{Guid.NewGuid():N}");
    private readonly CompanyLocator locator = new();

    [Fact]
    public void LocatesFromTheCompanyDirectory()
    {
        var company = Path.Combine(root, "Companies", "Acme");
        Directory.CreateDirectory(company);

        var located = locator.Locate(company);

        Assert.NotNull(located);
        Assert.Equal("Acme", located.CompanyName);
        Assert.Equal(Path.GetFullPath(root), located.ServerRoot);
    }

    [Fact]
    public void LocatesFromANestedDirectory()
    {
        var nested = Path.Combine(root, "Companies", "Acme", "Services", "Billing");
        Directory.CreateDirectory(nested);

        var located = locator.Locate(nested);

        Assert.NotNull(located);
        Assert.Equal("Acme", located.CompanyName);
    }

    [Fact]
    public void ReturnsNullOutsideACompany()
    {
        var outside = Path.Combine(root, "Elsewhere");
        Directory.CreateDirectory(outside);

        Assert.Null(locator.Locate(outside));
    }

    [Fact]
    public void ReturnsNullAtTheCompaniesRootItself()
    {
        // The Companies folder is not a company; scaffolding from here must fail loudly
        // rather than silently picking a neighbouring tenant.
        var companies = Path.Combine(root, "Companies");
        Directory.CreateDirectory(companies);

        Assert.Null(locator.Locate(companies));
    }

    public void Dispose()
    {
        if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
    }
}
