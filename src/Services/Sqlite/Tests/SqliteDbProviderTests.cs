using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Services.Companies.Data;
using Services.Companies.Models;
using Services.Companies;

namespace Services.Sqlite.Tests;

public sealed class SqliteDbProviderTests
{
    [Fact]
    public async Task ConfiguresWorkingContext()
    {
        var options = new DbContextOptionsBuilder<ProbeContext>();
        new SqliteDbProvider().Configure(options, "Data Source=:memory:");
        await using var database = new ProbeContext(options.Options);
        (await database.Database.EnsureCreatedAsync()).Should().BeTrue();
    }

    private sealed class ProbeContext(DbContextOptions<ProbeContext> options) : DbContext(options);

    [Fact]
    public async Task CompanyRegistryCrudRejectsDuplicateName()
    {
        var file = Path.Combine(Path.GetTempPath(), $"companyhq-sqlite-{Guid.NewGuid()}.db");
        try
        {
            var services = new ServiceCollection();
            var configuration = new ConfigurationManager { ["ConnectionStrings:Registry"] = $"Data Source={file}" };
            services.AddSingleton<IConfiguration>(configuration);
            new SqliteModule().Configure(services);
            new CompaniesModule().Configure(services);
            await using var provider = services.BuildServiceProvider();
            await using var scope = provider.CreateAsyncScope();
            var registry = scope.ServiceProvider.GetRequiredService<ICompanyRegistry>();
            var company = new RegisteredCompany { Name = "Acme", Slug = "acme", Host = "acme.test", AdminUrl = "https://acme.test/admin", DbProvider = "sqlite", DatabaseName = "companyhq", DeploymentStatus = "running" };
            await registry.Register(company);
            (await registry.GetCompanies()).Should().ContainSingle();
            var duplicate = async () => await registry.Register(new RegisteredCompany { Name = "Acme", Slug = "acme", Host = "other.test", AdminUrl = "https://other.test/admin", DbProvider = "sqlite", DatabaseName = "companyhq", DeploymentStatus = "running" });
            await duplicate.Should().ThrowAsync<InvalidOperationException>();
        }
        finally { Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools(); if (File.Exists(file)) File.Delete(file); }
    }
}
