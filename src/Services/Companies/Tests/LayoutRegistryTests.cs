using Services.Companies.Contracts;
using FluentAssertions;

namespace Services.Companies.Tests;

public sealed class LayoutRegistryTests
{
    [Fact]
    public void LatestRegistrationWinsAndUnregisterRestoresPrevious()
    {
        var registry = new LayoutRegistry();
        registry.Register("AdminLayout", typeof(string));
        registry.Register("AdminLayout", typeof(int));
        registry.Resolve("AdminLayout").Should().Be(typeof(int));
        registry.Unregister("AdminLayout", typeof(int)).Should().BeTrue();
        registry.Resolve("AdminLayout").Should().Be(typeof(string));
    }
}
