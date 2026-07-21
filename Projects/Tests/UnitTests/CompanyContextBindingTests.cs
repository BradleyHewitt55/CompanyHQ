using Abstractions.Companies;
using CompanyHQ.Services.Companies;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace UnitTests;

public sealed class CompanyContextBindingTests
{
    [Fact]
    public void EveryScopeStartsBoundToTheConfiguredCompany()
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(
            new Dictionary<string, string?> { ["CompanyHq:Company"] = "Acme" }).Build();
        var services = new ServiceCollection();

        new CompaniesPlugin().ConfigureServices(services, configuration);

        using var provider = services.BuildServiceProvider();
        using var first = provider.CreateScope();
        using var second = provider.CreateScope();
        var firstContext = first.ServiceProvider.GetRequiredService<ICompanyContext>();
        var secondContext = second.ServiceProvider.GetRequiredService<ICompanyContext>();

        Assert.Equal("Acme", firstContext.Current);
        Assert.Equal("Acme", secondContext.Current);
        Assert.NotSame(firstContext, secondContext);
    }

    [Fact]
    public void MissingCompanyFailsDuringRegistration()
    {
        var configuration = new ConfigurationBuilder().Build();

        Assert.Throws<InvalidOperationException>(() =>
            new CompaniesPlugin().ConfigureServices(new ServiceCollection(), configuration));
    }
}
