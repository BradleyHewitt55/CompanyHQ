using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Services.Companies.Contracts.Services;
using Services.Postgres.Providers;
using Services.Postgres.Services;
using Services.Sqlite;

namespace Services.Postgres.Tests;

public sealed class ServiceFolderConventionTests : IAsyncLifetime
{
    private readonly string databasePath = Path.Combine(Path.GetTempPath(), $"postgres-service-folder-{Guid.NewGuid():N}.db");
    private readonly ServiceProvider provider;

    public ServiceFolderConventionTests()
    {
        var services = new ServiceCollection();
        services.AddCompanyData(new SqliteDbProvider(), $"Data Source={databasePath}");
        provider = services.BuildServiceProvider();
    }

    public Task InitializeAsync() => Task.CompletedTask;

    public async Task DisposeAsync()
    {
        await provider.DisposeAsync();
        SqliteConnection.ClearAllPools();
        if (File.Exists(databasePath)) File.Delete(databasePath);
    }

    [Fact]
    public void ServiceStateDiResolvesTheServicesFolderImplementation() =>
        provider.GetRequiredService<IServiceStateStore>().Should().BeOfType<ServiceStateStore>();

    [Fact]
    public async Task StartupSeedsUnchangedEntityDefinitionsForACompanyDatabase()
    {
        var startup = new PostgresStartupTask();
        await startup.Run(provider);
        await startup.Run(provider);

        var factory = provider.GetRequiredService<IDbContextFactory<CompanyDbContext>>();
        await using var database = await factory.CreateDbContextAsync();
        (await database.EntityDefinitions.OrderBy(entity => entity.Name).Select(entity => entity.Name).ToListAsync())
            .Should().Equal("CompanyProfile", "User");
    }
}
