using System.Net;
using System.Text.RegularExpressions;
using Services.Companies.Contracts;
using Services.Auth;
using Services.Auth.Services;
using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.DependencyInjection;
using OverrideDashboard = Web.Admin.Overrides.Pages.Dashboard;

namespace Web.Tests;

public sealed partial class AdminOverrideIntegrationTests : IAsyncLifetime
{
    private string folder = null!;
    private WebApplicationFactory<Program> factory = null!;
    private string password = null!;

    public async Task InitializeAsync()
    {
        folder = Path.Combine(Path.GetTempPath(), "companyhq-overrides-" + Guid.NewGuid());
        Directory.CreateDirectory(Path.Combine(folder, "modules"));
        Directory.CreateDirectory(Path.Combine(folder, "keys"));
        Directory.CreateDirectory(Path.Combine(folder, "pages"));
        factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseSetting("ModulesPath", Path.Combine(folder, "modules"));
            builder.UseSetting("AdminOverridesPath", typeof(OverrideDashboard).Assembly.Location);
            builder.UseSetting("AdminPagesPath", Path.Combine(folder, "pages"));
            builder.UseSetting("ConnectionStrings:Company", $"Data Source={Path.Combine(folder, "company.db")}");
            builder.UseSetting("DataProtection:Path", Path.Combine(folder, "keys"));
            builder.UseSetting("Company:Id", Guid.NewGuid().ToString());
        });
        _ = factory.Services;
        await using var scope = factory.Services.CreateAsyncScope();
        password = (await scope.ServiceProvider.GetRequiredService<AdminUserSeederService>().SeedOwner("override@test.local")).Password!;
    }

    [Fact]
    public async Task CompanyHtmlOverrideRoundTripsWithoutEscapingItsPagesRoot()
    {
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost"), AllowAutoRedirect = false, HandleCookies = true });
        var loginHtml = await client.GetStringAsync("/auth/login");
        var token = TokenRegex().Match(loginHtml).Groups[1].Value;
        await client.PostAsync("/auth/login", new FormUrlEncodedContent(new Dictionary<string, string> { ["Username"] = "override@test.local", ["Password"] = password, ["__RequestVerificationToken"] = token }));
        var file = Path.Combine(folder, "pages", "users.html");
        await File.WriteAllTextAsync(file, "<h1>company-a-users-marker</h1>");
        (await client.GetStringAsync("/admin/users")).Should().Contain("company-a-users-marker");
        File.Delete(file);
        (await client.GetStringAsync("/admin/users")).Should().Contain("data-page=\"users\"").And.NotContain("company-a-users-marker");
        (await client.GetAsync("/admin/../company.db")).StatusCode.Should().NotBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task OverrideRendersAndRemovingRegistrationRestoresDefault()
    {
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost"), AllowAutoRedirect = false, HandleCookies = true });
        var loginHtml = await client.GetStringAsync("/auth/login");
        var token = TokenRegex().Match(loginHtml).Groups[1].Value;
        var login = await client.PostAsync("/auth/login", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["Username"] = "override@test.local", ["Password"] = password, ["__RequestVerificationToken"] = token
        }));
        login.StatusCode.Should().Be(HttpStatusCode.Redirect);

        (await client.GetStringAsync("/admin/dashboard")).Should().Contain("integration-override");
        factory.Services.GetRequiredService<ComponentRegistry>().Unregister("dashboard", typeof(OverrideDashboard)).Should().BeTrue();
        (await client.GetStringAsync("/admin/dashboard")).Should().Contain("data-page=\"dashboard\"").And.NotContain("integration-override");
    }

    public Task DisposeAsync()
    {
        factory.Dispose();
        SqliteConnection.ClearAllPools();
        Directory.Delete(folder, true);
        return Task.CompletedTask;
    }

    [GeneratedRegex("name=\"__RequestVerificationToken\" value=\"([^\"]+)\"")]
    private static partial Regex TokenRegex();
}
