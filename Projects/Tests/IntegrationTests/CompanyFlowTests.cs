using System.Text.Json;
using Abstractions.Companies;
using Abstractions.Data;
using Abstractions.Users;
using CompanyHQ.Services.Companies.Services;
using Microsoft.Extensions.DependencyInjection;

namespace IntegrationTests;

[Collection(PostgresCollection.Name)]
public sealed class CompanyFlowTests(PostgresFixture fixture)
{
    [Fact]
    public async Task FullLocalFlowCreatesCompanyDatabaseAndHashedAdmin()
    {
        var company = $"Managed{Guid.NewGuid():N}";
        fixture.TrackManagedCompany(company);
        await using var provider = fixture.CreateProvider();
        var console = new ScriptedConsole(
            ["Ada", "Lovelace", "ada@example.com", "+27123", "1"],
            ["correct horse", "correct horse"]);

        await provider.GetRequiredService<CompanyCreation>().CreateAsync(company, console);

        var companyDirectory = provider.GetRequiredService<IServerPaths>().CompanyDirectory(company);
        Assert.True(Directory.Exists(companyDirectory));
        Assert.True(Directory.Exists(Path.Combine(companyDirectory, "Services")));
        using (var document = JsonDocument.Parse(await File.ReadAllTextAsync(Path.Combine(companyDirectory, "db.json"))))
        {
            Assert.True(document.RootElement.GetProperty("managed").GetBoolean());
        }

        using var scope = provider.CreateScope();
        scope.ServiceProvider.GetRequiredService<ICompanyContext>().Current = company;
        var users = await scope.ServiceProvider.GetRequiredService<IQueryService<User>>().ListAsync();
        var admin = Assert.Single(users);
        Assert.Equal(Roles.Admin, admin.Role);
        Assert.NotEqual("correct horse", admin.PasswordHash);
        Assert.NotEmpty(admin.PasswordHash);
    }

    [Fact]
    public async Task ExternalDatabaseFlowRecordsUnmanagedCredentials()
    {
        var company = $"External{Guid.NewGuid():N}";
        var connectionString = await fixture.CreateExternalDatabaseAsync();
        await using var provider = fixture.CreateProvider();
        var console = new ScriptedConsole(
            ["Grace", "Hopper", "grace@example.com", "+27456", "2", connectionString],
            ["compiler", "compiler"]);

        await provider.GetRequiredService<CompanyCreation>().CreateAsync(company, console);

        var path = Path.Combine(provider.GetRequiredService<IServerPaths>().CompanyDirectory(company), "db.json");
        using var document = JsonDocument.Parse(await File.ReadAllTextAsync(path));
        Assert.False(document.RootElement.GetProperty("managed").GetBoolean());
        Assert.Equal(connectionString, document.RootElement.GetProperty("connectionString").GetString());
    }

    [Fact]
    public async Task ScaffolderCreatesTemplateAndEnforcesCompanyBoundary()
    {
        var company = $"Scaffold{Guid.NewGuid():N}";
        await using var provider = fixture.CreateProvider();
        await provider.GetRequiredService<ICompanyRegistry>().RegisterAsync(company);

        using (var scope = provider.CreateScope())
        {
            scope.ServiceProvider.GetRequiredService<ICompanyContext>().Current = company;
            var scaffolder = scope.ServiceProvider.GetRequiredService<IServiceScaffolder>();
            await scaffolder.ScaffoldAsync("Billing");
            await Assert.ThrowsAsync<InvalidOperationException>(() => scaffolder.ScaffoldAsync("Billing"));
        }

        var serviceDirectory = Path.Combine(
            provider.GetRequiredService<IServerPaths>().CompanyDirectory(company), "Services", "Billing");
        Assert.True(File.Exists(Path.Combine(serviceDirectory, "Settings", "service.json")));
        Assert.True(File.Exists(Path.Combine(serviceDirectory, "README.md")));
        Assert.True(File.Exists(Path.Combine(serviceDirectory, "Billing.csproj")));
        Assert.True(File.Exists(Path.Combine(serviceDirectory, "BillingPlugin.cs")));

        using var processBoundScope = provider.CreateScope();
        Assert.Equal(
            "Integration",
            processBoundScope.ServiceProvider.GetRequiredService<ICompanyContext>().Current);
    }
}
