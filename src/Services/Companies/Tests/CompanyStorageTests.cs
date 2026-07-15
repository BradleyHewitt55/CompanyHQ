using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Services.Companies.Contracts.Data;
using Services.Companies.Data;
using Services.Companies.Models;
using Services.Postgres.Providers;
using Services.Postgres;
using Services.Orders.Models;
using Services.Sqlite;
using Testcontainers.PostgreSql;

namespace Services.Companies.Tests;

public sealed class CompanyStorageTests
{
    [Fact]
    public async Task ProfileAndRegistryUseSeparateSqliteDatabases()
    {
        var folder = Path.Combine(Path.GetTempPath(), "companyhq-storage-" + Guid.NewGuid());
        Directory.CreateDirectory(folder);
        try
        {
            await using var provider = Build("sqlite", $"Data Source={Path.Combine(folder, "company.db")}", Path.Combine(folder, "registry.db"));
            await Exercise(provider);
            File.Exists(Path.Combine(folder, "company.db")).Should().BeTrue();
            File.Exists(Path.Combine(folder, "registry.db")).Should().BeTrue();
        }
        finally { Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools(); Directory.Delete(folder, true); }
    }

    [Fact, Trait("Category", "Docker")]
    public async Task ProfileUsesPostgresWhileRegistryStaysSqlite()
    {
        await using var postgres = new PostgreSqlBuilder("postgres:17-alpine").Build();
        await postgres.StartAsync();
        var registry = Path.Combine(Path.GetTempPath(), $"companyhq-registry-{Guid.NewGuid()}.db");
        try { await using var provider = Build("postgres", postgres.GetConnectionString(), registry); await Exercise(provider); File.Exists(registry).Should().BeTrue(); }
        finally { Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools(); if (File.Exists(registry)) File.Delete(registry); }
    }

    private static ServiceProvider Build(string providerName, string companyConnection, string registryFile)
    {
        var services = new ServiceCollection();
        var configuration = new ConfigurationManager();
        configuration["Database:Provider"] = providerName;
        configuration["ConnectionStrings:Company"] = companyConnection;
        configuration["ConnectionStrings:Registry"] = $"Data Source={registryFile}";
        services.AddSingleton<IConfiguration>(configuration);
        services.AddSingleton<IDbProvider, SqliteDbProvider>();
        services.AddSingleton<IDbProvider, PostgresDbProvider>();
        new CompaniesModule().Configure(services);
        services.AddCompanyData(providerName, companyConnection, types => types.Add(typeof(Order)));
        return services.BuildServiceProvider();
    }

    private static async Task Exercise(ServiceProvider provider)
    {
        await using var scope = provider.CreateAsyncScope();
        var registry = scope.ServiceProvider.GetRequiredService<ICompanyRegistry>();
        await registry.Register(new RegisteredCompany { Name = "Acme", Slug = "acme", Host = "acme.test", AdminUrl = "https://acme.test/admin", DbProvider = "postgres", DatabaseName = "companyhq", DeploymentStatus = "running" });
        var profiles = scope.ServiceProvider.GetRequiredService<ICompanyProfileStore>();
        await profiles.Save(new CompanyProfile { Id = Guid.NewGuid(), Name = "Acme", Email = "hello@acme.test", PrimaryColor = "#123456" });
        var companyDatabase = scope.ServiceProvider.GetRequiredService<CompanyDbContext>();
        await companyDatabase.Database.EnsureCreatedAsync();
        companyDatabase.Set<Order>().Add(new Order { Id = Guid.NewGuid(), Slug = "acme-order", Total = 42m });
        await companyDatabase.SaveChangesAsync();
        (await profiles.Get())!.Email.Should().Be("hello@acme.test");
        companyDatabase.Set<Order>().Local.Should().ContainSingle();
        (await registry.GetCompanies()).Should().ContainSingle();
    }

    [Fact]
    public async Task RegistrySchemaContainsOnlyRoutingAndDeploymentMetadata()
    {
        var registry = Path.Combine(Path.GetTempPath(), $"companyhq-registry-{Guid.NewGuid()}.db");
        var company = Path.Combine(Path.GetTempPath(), $"companyhq-company-{Guid.NewGuid()}.db");
        try
        {
            await using var provider = Build("sqlite", $"Data Source={company}", registry);
            await Exercise(provider);

            await using var connection = new SqliteConnection($"Data Source={registry}");
            await connection.OpenAsync();
            await using var tablesCommand = connection.CreateCommand();
            tablesCommand.CommandText = "SELECT name FROM sqlite_master WHERE type = 'table'";
            await using var reader = await tablesCommand.ExecuteReaderAsync();
            var tables = new List<string>();
            while (await reader.ReadAsync()) tables.Add(reader.GetString(0));

            tables.Should().Contain("Companies");
            tables.Should().NotContain(table => table.Contains("Profile", StringComparison.OrdinalIgnoreCase)
                || table.Contains("AspNet", StringComparison.OrdinalIgnoreCase)
                || table.Contains("Order", StringComparison.OrdinalIgnoreCase)
                || table.Contains("Customer", StringComparison.OrdinalIgnoreCase)
                || table.Contains("Service", StringComparison.OrdinalIgnoreCase));

            await using var columnsCommand = connection.CreateCommand();
            columnsCommand.CommandText = "PRAGMA table_info(Companies)";
            await using var columnsReader = await columnsCommand.ExecuteReaderAsync();
            var columns = new List<string>();
            while (await columnsReader.ReadAsync()) columns.Add(columnsReader.GetString(1));
            columns.Should().Contain(["Id", "Name", "Host", "DatabaseName", "DeploymentStatus"]);
            columns.Should().NotContain(column => column.Contains("Profile", StringComparison.OrdinalIgnoreCase)
                || column.Contains("Password", StringComparison.OrdinalIgnoreCase)
                || column.Contains("Order", StringComparison.OrdinalIgnoreCase)
                || column.Contains("Customer", StringComparison.OrdinalIgnoreCase));
        }
        finally
        {
            Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
            if (File.Exists(registry)) File.Delete(registry);
            if (File.Exists(company)) File.Delete(company);
        }
    }
}
