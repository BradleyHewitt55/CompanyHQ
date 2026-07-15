using System.Net;
using System.Net.Http.Json;
using Services.Postgres;
using Services.Companies.Contracts.Services;
using Services.Postgres.Providers;
using Services.Companies.Models;
using Services.Companies.Data;
using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Services.Postgres.Tests;

public sealed class RootRegistryTests
{
    [Fact]
    public async Task SqliteRegistryCrudRejectsDuplicateName()
    {
        var file = Path.Combine(Path.GetTempPath(), $"companyhq-registry-{Guid.NewGuid()}.db");
        try
        {
            var services = new ServiceCollection();
            var configuration = new Microsoft.Extensions.Configuration.ConfigurationManager();
            configuration["ConnectionStrings:Registry"] = $"Data Source={file}";
            services.AddSingleton<Microsoft.Extensions.Configuration.IConfiguration>(configuration);
            services.AddSingleton<global::Services.Companies.Contracts.Data.IDbProvider, global::Services.Sqlite.SqliteDbProvider>();
            new global::Services.Companies.CompaniesModule().Configure(services);
            await using var provider = services.BuildServiceProvider();
            var factory = provider.GetRequiredService<IDbContextFactory<global::Services.Companies.Data.CompaniesDbContext>>();
            await using (var context = await factory.CreateDbContextAsync()) await context.Database.EnsureCreatedAsync();
            await using var scope = provider.CreateAsyncScope();
            var registry = scope.ServiceProvider.GetRequiredService<ICompanyRegistry>();
            await registry.Register(Company("Acme"));
            (await registry.GetCompanies()).Should().ContainSingle(item => item.Name == "Acme");
            var duplicate = async () => await registry.Register(Company("Acme"));
            await duplicate.Should().ThrowAsync<InvalidOperationException>();
        }
        finally { Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools(); if (File.Exists(file)) File.Delete(file); }
    }

    [Fact]
    public async Task RegistrationApiRequiresTokenAndCompanyAppearsInUi()
    {
        var folder = Path.Combine(Path.GetTempPath(), "companyhq-root-" + Guid.NewGuid());
        Directory.CreateDirectory(folder);
        using var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseSetting("ConnectionStrings:Registry", $"Data Source={Path.Combine(folder, "companies.db")}");
            builder.UseSetting("Registry:Token", "test-token");
        });
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        (await client.PostAsJsonAsync("/api/companies", Company("NoToken"))).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/companies") { Content = JsonContent.Create(Company("Acme")) };
        request.Headers.Add("X-CompanyHQ-Token", "test-token");
        (await client.SendAsync(request)).StatusCode.Should().Be(HttpStatusCode.Created);
        var protectedPage = await client.GetAsync("/admin/companies");
        protectedPage.StatusCode.Should().Be(HttpStatusCode.Redirect);
        protectedPage.Headers.Location!.AbsolutePath.Should().Be("/auth/login");
        await using (var scope = factory.Services.CreateAsyncScope())
            (await scope.ServiceProvider.GetRequiredService<ICompanyRegistry>().GetCompanies()).Should().ContainSingle(company => company.Name == "Acme");
        factory.Dispose();
        Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
        Directory.Delete(folder, true);
    }

    private static RegisteredCompany Company(string name) => new() { Name = name, Slug = name.ToLowerInvariant(), Host = $"{name.ToLowerInvariant()}.test", AdminUrl = $"https://{name.ToLowerInvariant()}.test/admin", DbProvider = "postgres", DatabaseName = "companyhq", DeploymentStatus = "running" };
}
