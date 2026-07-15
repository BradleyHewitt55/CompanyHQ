using FluentAssertions;
namespace Services.Sales.Tests;
public sealed class SalesTests { [Fact] public void ModuleOwnsSalesManifest() => new SalesModule().Manifest.Id.Should().Be("sales"); }
