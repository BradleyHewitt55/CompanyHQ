using Services.Companies.Contracts;
using FluentAssertions;

namespace Services.Companies.Tests;

public sealed class ComponentRegistryTests
{
    private sealed class Missing;
    private sealed class Dashboard;
    private sealed class OverrideDashboard;

    [Fact]
    public void RegisterAndResolveUsesStableCaseInsensitiveName()
    {
        var registry = new ComponentRegistry(typeof(Missing));
        registry.Register("Dashboard", typeof(Dashboard));
        registry.Resolve("/dashboard/").Should().Be(typeof(Dashboard));
    }

    [Fact]
    public void UnknownNameReturnsFallbackMarker()
    {
        var registry = new ComponentRegistry(typeof(Missing));
        registry.Resolve("nope").Should().Be(typeof(Missing));
    }

    [Fact]
    public void LaterRegistrationWinsAndCanBeRemoved()
    {
        var registry = new ComponentRegistry(typeof(Missing));
        registry.Register("dashboard", typeof(Dashboard));
        registry.Register("dashboard", typeof(OverrideDashboard));
        registry.Resolve("dashboard").Should().Be(typeof(OverrideDashboard));
        registry.Unregister("dashboard", typeof(OverrideDashboard)).Should().BeTrue();
        registry.Resolve("dashboard").Should().Be(typeof(Dashboard));
    }
}
