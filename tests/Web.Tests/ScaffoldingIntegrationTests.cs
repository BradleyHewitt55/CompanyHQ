using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Services.Auth;
using Services.Auth.Models;
using Services.Companies.Contracts.Services;

namespace Web.Tests;

[Collection(AuthCollection.Name)]
public sealed class ScaffoldingIntegrationTests(AuthHostFixture fixture)
{
    [Fact]
    public async Task ApiEnforcesAuthenticationRoleAndCsrfAndOwnerCreatesChild()
    {
        using var anonymous = fixture.CreateClient();
        (await anonymous.GetAsync("/api/scaffolding/services")).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await anonymous.PostAsJsonAsync("/api/scaffolding/services", new { name = "Invoices", parent = "Sales" })).StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        const string viewerName = "viewer-scaffolding@test.local";
        const string viewerPassword = "Viewer-Secure!12345";
        await using (var scope = fixture.Factory.Services.CreateAsyncScope())
        {
            var users = scope.ServiceProvider.GetRequiredService<UserManager<CompanyUser>>();
            if (await users.FindByNameAsync(viewerName) is null)
            {
                var viewer = new CompanyUser { UserName = viewerName, Email = viewerName };
                (await users.CreateAsync(viewer, viewerPassword)).Succeeded.Should().BeTrue();
                (await users.AddToRoleAsync(viewer, AuthRoles.Viewer)).Succeeded.Should().BeTrue();
            }
        }
        using var viewerClient = fixture.CreateClient();
        (await fixture.Login(viewerClient, viewerName, viewerPassword)).StatusCode.Should().Be(HttpStatusCode.Redirect);
        var viewerToken = await fixture.GetAntiforgeryToken(viewerClient);
        using var forbidden = new HttpRequestMessage(HttpMethod.Post, "/api/scaffolding/services") { Content = JsonContent.Create(new { name = "Invoices", parent = "Sales" }) };
        forbidden.Headers.Add("RequestVerificationToken", viewerToken);
        (await viewerClient.SendAsync(forbidden)).StatusCode.Should().Be(HttpStatusCode.Forbidden);

        using var owner = fixture.CreateClient();
        (await fixture.Login(owner, fixture.OwnerUsername, fixture.OwnerPassword)).StatusCode.Should().Be(HttpStatusCode.Redirect);
        (await owner.PostAsJsonAsync("/api/scaffolding/services", new { name = "Invoices", parent = "Sales" })).StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var token = await fixture.GetAntiforgeryToken(owner);
        using var create = new HttpRequestMessage(HttpMethod.Post, "/api/scaffolding/services") { Content = JsonContent.Create(new { name = "Invoices", parent = "Sales" }) };
        create.Headers.Add("RequestVerificationToken", token);
        var created = await owner.SendAsync(create);
        created.StatusCode.Should().Be(HttpStatusCode.Created);
        Directory.Exists(Path.Combine(fixture.RootFolder, "src", "Services", "Sales", "Services", "Invoices")).Should().BeTrue();
        (await owner.GetAsync("/api/services/invoices")).StatusCode.Should().Be(HttpStatusCode.OK);
        var servicesHtml = await owner.GetStringAsync("/admin/services");
        servicesHtml.Should().Contain("Invoices");

        var broken = Path.Combine(fixture.RootFolder, "broken-service");
        Directory.CreateDirectory(broken);
        await File.WriteAllTextAsync(Path.Combine(broken, "Broken.csproj"), "<Project Sdk=\"Microsoft.NET.Sdk\"><PropertyGroup><TargetFramework>net10.0</TargetFramework></PropertyGroup></Project>");
        await File.WriteAllTextAsync(Path.Combine(broken, "Broken.cs"), "this does not compile");
        var loader = fixture.Factory.Services.GetRequiredService<IRuntimeServiceLoader>();
        (await loader.BuildAndLoad(broken)).Loaded.Should().BeFalse();
        (await owner.GetAsync("/health")).StatusCode.Should().Be(HttpStatusCode.OK);
    }
}
