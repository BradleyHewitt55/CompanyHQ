using System.Net;
using Web.Modules;
using Services.Companies.Contracts.Services;
using Services.Orders;
using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Testcontainers.PostgreSql;
using Services.Auth;
using System.Text.RegularExpressions;

namespace Web.Tests;

[Trait("Category", "Docker")]
public sealed class ModuleStateIntegrationTests : IAsyncLifetime
{
    private readonly PostgreSqlContainer postgres = new PostgreSqlBuilder("postgres:17-alpine").Build();
    private string folder = null!;
    private WebApplicationFactory<Program> factory = null!;
    private HttpClient client = null!;

    public async Task InitializeAsync()
    {
        await postgres.StartAsync();
        folder = Path.Combine(Path.GetTempPath(), "companyhq-host-" + Guid.NewGuid());
        Directory.CreateDirectory(folder);
        var module = typeof(OrdersModule).Assembly.Location;
        File.Copy(module, Path.Combine(folder, Path.GetFileName(module)));
        File.Copy(Path.Combine(Path.GetDirectoryName(module)!, "orders.json"), Path.Combine(folder, "orders.json"));
        factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseSetting("ModulesPath", folder);
            builder.UseSetting("Database:Provider", "postgres");
            builder.UseSetting("ConnectionStrings:Company", postgres.GetConnectionString());
        });
        client = factory.CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost"), AllowAutoRedirect = false, HandleCookies = true });
        await using var scope = factory.Services.CreateAsyncScope();
        var owner = await scope.ServiceProvider.GetRequiredService<AdminUserSeeder>().SeedOwner("module-owner@test");
        var html = await client.GetStringAsync("/auth/login");
        var token = Regex.Match(html, "name=\"__RequestVerificationToken\" value=\"([^\"]+)\"").Groups[1].Value;
        (await client.PostAsync("/auth/login", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["Username"] = "module-owner@test", ["Password"] = owner.Password!, ["__RequestVerificationToken"] = token
        }))).StatusCode.Should().Be(HttpStatusCode.Redirect);
    }

    [Fact]
    public async Task EnabledStateControlsApiPageAndNavigationWithoutRestartWhileHiddenModuleStaysHidden()
    {
        factory.Services.GetRequiredService<ModuleCatalog>().Modules.Should().Contain(module => module.Manifest.Id == "orders");
        (await client.GetAsync("/api/services/orders")).StatusCode.Should().Be(HttpStatusCode.OK);
        (await client.GetAsync("/api/services/auth")).StatusCode.Should().Be(HttpStatusCode.NotFound);
        var services = await client.GetStringAsync("/admin/services");
        services.Should().Contain("Manage customer orders").And.NotContain("Authentication");
        (await client.GetStringAsync("/admin/dashboard")).Should().Contain("href=\"/admin/services/orders/index\"");
        (await client.GetStringAsync("/admin/services/orders/index")).Should().Contain("data-page=\"orders\"");

        await factory.Services.GetRequiredService<IServiceStateStore>().SetEnabled("orders", false);
        (await client.GetAsync("/api/services/orders")).StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await client.GetStringAsync("/admin/services/orders/index")).Should().Contain("data-page=\"not-found\"");
        (await client.GetStringAsync("/admin/dashboard")).Should().NotContain("href=\"/admin/services/orders/index\"");
    }

    public async Task DisposeAsync()
    {
        client.Dispose();
        factory.Dispose();
        Directory.Delete(folder, true);
        await postgres.DisposeAsync();
    }
}
