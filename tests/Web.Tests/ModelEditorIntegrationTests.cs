using System.Net;
using System.Net.Http.Json;
using Services.Postgres;
using Services.Companies.Contracts.Services;
using Services.Auth.Models;
using Services.Companies.Contracts.Metadata;
using Services.Orders.Models;
using FluentAssertions;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;

namespace Web.Tests;

[Collection(AuthCollection.Name)]
public sealed class ModelEditorIntegrationTests(AuthHostFixture fixture)
{
    [Fact]
    public async Task UserFieldAndOrderLinkPersistAndRenderWhileBaseColumnChangeIsRejected()
    {
        using var client = fixture.CreateClient();
        (await fixture.Login(client, fixture.OwnerUsername, fixture.OwnerPassword)).StatusCode.Should().Be(HttpStatusCode.Redirect);
        await using var scope = fixture.Factory.Services.CreateAsyncScope();
        var user = (await scope.ServiceProvider.GetRequiredService<UserManager<CompanyUser>>().FindByNameAsync(fixture.OwnerUsername))!;

        (await client.PostAsJsonAsync("/api/admin/model/fields", new AddFieldRequest("User", "CustomerTier", ExtensionFieldType.String))).StatusCode.Should().Be(HttpStatusCode.OK);
        (await client.PutAsJsonAsync($"/api/admin/model/users/{user.Id}/extensions/CustomerTier", "Gold")).StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await client.PostAsJsonAsync("/api/admin/model/links", new AddLinkRequest("User", "Order", LinkCardinality.OneToMany))).StatusCode.Should().Be(HttpStatusCode.OK);
        await scope.ServiceProvider.GetRequiredService<IEntityService<Order>>().Save(new Order { Slug = "linked-order", Total = 25, UserId = user.Id });

        var html = await client.GetStringAsync("/admin/users");
        html.Should().Contain("CustomerTier").And.Contain("Gold").And.Contain("linked-order");
        (await client.PostAsJsonAsync("/api/admin/model/fields", new AddFieldRequest("User", "UserName", ExtensionFieldType.String))).StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }
}
