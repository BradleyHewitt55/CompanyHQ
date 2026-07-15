using System.Net;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Services.Companies.Contracts.Services;

namespace Web.Tests;

[Collection(AuthCollection.Name)]
public sealed class NestedServiceIntegrationTests(AuthHostFixture fixture)
{
    [Fact]
    public async Task SalesOwnsChildrenAndParentStateGatesSubtreeWithoutLosingChildState()
    {
        using var client = fixture.CreateClient();
        (await fixture.Login(client, fixture.OwnerUsername, fixture.OwnerPassword)).StatusCode.Should().Be(HttpStatusCode.Redirect);
        var dashboard = await client.GetStringAsync("/admin/dashboard");
        dashboard.Should().Contain("data-nav=\"Sales\"").And.Contain("data-nav=\"Orders\"").And.Contain("data-nav=\"Quotes\"").And.Contain("data-nav=\"Products\"");
        (await client.GetAsync("/api/services/orders")).StatusCode.Should().Be(HttpStatusCode.OK);

        var states = fixture.Factory.Services.GetRequiredService<IServiceStateStore>();
        await states.SetEnabled("sales", false);
        (await client.GetAsync("/api/services/orders")).StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await client.GetStringAsync("/admin/dashboard")).Should().NotContain("data-nav=\"Orders\"");

        var token = await fixture.GetAntiforgeryToken(client);
        using var enableChild = new HttpRequestMessage(HttpMethod.Put, "/api/admin/services/orders/enabled?enabled=true");
        enableChild.Headers.Add("RequestVerificationToken", token);
        (await client.SendAsync(enableChild)).StatusCode.Should().Be(HttpStatusCode.Conflict);

        await states.SetEnabled("sales", true);
        states.IsEnabled("orders").Should().BeTrue();
        (await client.GetAsync("/api/services/orders")).StatusCode.Should().Be(HttpStatusCode.OK);
    }
}
