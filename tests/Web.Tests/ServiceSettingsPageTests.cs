using System.Net;
using FluentAssertions;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Services.Auth.Models;
using Services.Companies.Contracts;
using Services.Companies.Contracts.Services;
using Services.Orders;
using Services.Orders.Pages;
using Web.Admin.Components;
using Web.Modules;

namespace Web.Tests;

[Collection(AuthCollection.Name)]
public sealed class ServiceSettingsPageTests(AuthHostFixture fixture)
{
    [Fact]
    public async Task SettingsComponentResolvesOnlyWhileItsServiceIsEnabled()
    {
        var registry = fixture.Factory.Services.GetRequiredService<ComponentRegistry>();
        var states = fixture.Factory.Services.GetRequiredService<IServiceStateStore>();
        await states.SetEnabled("orders", true);
        registry.Resolve("services/orders/settings").Should().Be(typeof(OrdersSettingsPage));
        registry.Resolve("services/orders/settings/general").Should().Be(typeof(OrdersSettingsPage));

        await states.SetEnabled("orders", false);

        registry.Resolve("services/orders/settings").Should().Be(typeof(NotFoundMarker));
    }

    [Fact]
    public async Task DisabledServiceSettingsRouteReturnsNotFound()
    {
        using var client = fixture.CreateClient();
        await fixture.Login(client, fixture.OwnerUsername, fixture.OwnerPassword);
        var states = fixture.Factory.Services.GetRequiredService<IServiceStateStore>();
        await states.SetEnabled("orders", false);

        (await client.GetAsync("/admin/services/orders/settings")).StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task ViewerIsDeniedAtSettingsRouteOnTheServer()
    {
        var states = fixture.Factory.Services.GetRequiredService<IServiceStateStore>();
        await states.SetEnabled("orders", true);
        const string username = "settings-viewer@company.test";
        const string password = "ValidPassword!234";
        await using (var scope = fixture.Factory.Services.CreateAsyncScope())
        {
            var users = scope.ServiceProvider.GetRequiredService<UserManager<CompanyUser>>();
            if (await users.FindByNameAsync(username) is null)
            {
                var user = new CompanyUser { UserName = username, Email = username, DisplayName = username };
                (await users.CreateAsync(user, password)).Succeeded.Should().BeTrue();
                (await users.AddToRoleAsync(user, AuthRoles.Viewer)).Succeeded.Should().BeTrue();
            }
        }
        using var client = fixture.CreateClient();
        await fixture.Login(client, username, password);

        (await client.GetAsync("/admin/services/orders/settings")).StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }
}
