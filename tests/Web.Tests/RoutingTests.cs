using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;

namespace Web.Tests;

public sealed class RoutingTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly HttpClient client;

    public RoutingTests(WebApplicationFactory<Program> factory) => client = factory.CreateClient();

    [Fact]
    public async Task PublicRouteRendersWithoutAuthentication()
    {
        var response = await client.GetAsync("/");
        response.EnsureSuccessStatusCode();
        (await response.Content.ReadAsStringAsync()).Should().Contain("data-page=\"public-home\"");
    }

    [Theory]
    [InlineData("/admin")]
    [InlineData("/admin/dashboard")]
    [InlineData("/admin/companies")]
    [InlineData("/admin/nonexistent")]
    public async Task AdminRoutesRequireAuthentication(string path)
    {
        var response = await client.GetAsync(path);
        response.RequestMessage!.RequestUri!.AbsolutePath.Should().Be("/auth/login");
    }

    [Fact]
    public async Task LegacyCompanyRouteRedirectsToAdminCompanies()
    {
        using var noRedirectClient = new WebApplicationFactory<Program>().CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        var response = await noRedirectClient.GetAsync("/companies");
        response.StatusCode.Should().Be(System.Net.HttpStatusCode.MovedPermanently);
        response.Headers.Location.Should().Be("/admin/companies");
    }
}
