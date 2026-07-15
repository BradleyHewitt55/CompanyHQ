using Services.Companies.Contracts.Services;
using FluentAssertions;
using Services.Scaffolding;

namespace Cli.Tests;

public sealed class NewServiceTests
{
    [Fact]
    public void NewServiceProducesConventionTreeAndValidManifest()
    {
        var root = Path.Combine(Path.GetTempPath(), "companyhq-service-" + Guid.NewGuid());
        try
        {
            var parent = Path.Combine(root, "src", "Services", "Sales");
            Directory.CreateDirectory(Path.Combine(parent, "Services"));
            File.WriteAllText(Path.Combine(parent, "sales.json"), "{\"id\":\"sales\",\"name\":\"Sales\",\"version\":\"1.0.0\",\"author\":\"test\",\"icon\":\"sales\",\"color\":\"#000\",\"tags\":[],\"description\":\"Sales\",\"menu\":[]}");
            var directory = ServiceScaffolder.Generate(root, "Invoices", "Sales");
            foreach (var folder in new[] { "Api", "Models", "Pages", "Settings", "Services", "Tests" }) Directory.Exists(Path.Combine(directory, folder)).Should().BeTrue();
            File.Exists(Path.Combine(directory, "Invoices.csproj")).Should().BeTrue();
            File.ReadAllText(Path.Combine(directory, "README.md")).Should().NotBeNullOrWhiteSpace();
            var manifest = ServiceManifest.Parse(File.ReadAllText(Path.Combine(directory, "invoices.json")));
            manifest.Hidden.Should().BeFalse();
            manifest.Menu.Should().ContainSingle(item => item.Url == "/admin/services/invoices/index");
            var collision = () => ServiceScaffolder.Generate(root, "Invoices", "Sales");
            collision.Should().Throw<InvalidOperationException>();
        }
        finally { if (Directory.Exists(root)) Directory.Delete(root, true); }
    }

    [Theory]
    [InlineData("")]
    [InlineData("sales")]
    [InlineData("Bad Name")]
    public void InvalidNamesAreRejected(string name) => ServiceScaffolder.IsValidName(name).Should().BeFalse();
}
