using Services.Companies.Contracts.Services;
using FluentAssertions;

namespace Services.Companies.Tests;

public sealed class ServiceManifestTests
{
    [Fact]
    public void ParsesOrderHiddenAndMenuAndRejectsMissingMenuUrl()
    {
        var manifest = ServiceManifest.Parse("""{"id":"sales","name":"Sales","version":"1","author":"A","icon":"x","color":"x","tags":[],"description":"x","order":3,"hidden":true,"menu":[{"label":"Orders","url":"/orders"}]}""");
        manifest.Order.Should().Be(3);
        manifest.Hidden.Should().BeTrue();
        manifest.Menu.Should().ContainSingle(item => item.Label == "Orders" && item.Url == "/orders");
        var invalid = () => ServiceManifest.Parse("""{"id":"sales","name":"Sales","version":"1","author":"A","icon":"x","color":"x","tags":[],"description":"x","menu":[{"label":"Orders"}]}""");
        invalid.Should().Throw<ServiceManifestException>().WithMessage("*url*");
    }
    [Fact]
    public void ValidManifestDeserializesAndHiddenDefaultsFalse()
    {
        var manifest = ServiceManifest.Parse("""{"id":"orders","name":"Orders","version":"1.0.0","author":"CompanyHQ","icon":"bag","color":"#fff","tags":["commerce"],"description":"Orders"}""");
        manifest.Id.Should().Be("orders");
        manifest.Hidden.Should().BeFalse();
    }

    [Fact]
    public void MissingRequiredFieldsHaveClearError()
    {
        var action = () => ServiceManifest.Parse("""{"id":"orders","name":"Orders"}""");
        action.Should().Throw<ServiceManifestException>().WithMessage("*version*author*");
    }
}
