using System.Net;
using Xunit;

namespace Northstar.Tests;

public sealed class HostTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    [Fact]
    public async Task The_host_starts_on_this_companys_migrations()
    {
        using var response = await factory.CreateClient().GetAsync("/health");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}
