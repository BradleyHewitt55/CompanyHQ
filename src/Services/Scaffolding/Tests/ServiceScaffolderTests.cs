using FluentAssertions;
using Services.Companies.Contracts.Services;
using System.Diagnostics;

namespace Services.Scaffolding.Tests;

public sealed class ServiceScaffolderTests : IDisposable
{
    private readonly string root = Path.Combine(Path.GetTempPath(), "companyhq-scaffolding-" + Guid.NewGuid());

    public ServiceScaffolderTests()
    {
        var parent = Path.Combine(root, "src", "Services", "Sales");
        Directory.CreateDirectory(Path.Combine(parent, "Services"));
        File.WriteAllText(Path.Combine(parent, "sales.json"), "{\"id\":\"sales\",\"name\":\"Sales\",\"version\":\"1.0.0\",\"author\":\"test\",\"icon\":\"sales\",\"color\":\"#000\",\"tags\":[],\"description\":\"Sales\",\"menu\":[]}");
        Directory.CreateDirectory(Path.Combine(root, "lib"));
        File.Copy(typeof(ServiceManifest).Assembly.Location, Path.Combine(root, "lib", "Companies.dll"));
        File.Copy(Path.Combine(FindRepositoryRoot(), "Directory.Packages.props"), Path.Combine(root, "Directory.Packages.props"));
    }

    [Fact]
    public void GeneratesFullOwnedConventionAndCatalog()
    {
        var directory = ServiceScaffolder.Generate(root, "Invoices", "Sales");
        directory.Should().Be(Path.Combine(root, "src", "Services", "Sales", "Services", "Invoices"));
        foreach (var folder in new[] { "Api", "Models", "Pages", "Settings", "Services", "Tests" }) Directory.Exists(Path.Combine(directory, folder)).Should().BeTrue();
        File.Exists(Path.Combine(directory, "Tests", "Invoices.Tests.csproj")).Should().BeTrue();
        File.Exists(Path.Combine(directory, "Tests", "StarterTests.cs")).Should().BeTrue();
        ServiceManifest.Parse(File.ReadAllText(Path.Combine(directory, "invoices.json"))).Id.Should().Be("invoices");
        ServiceScaffolder.Catalog.Should().NotBeEmpty();
        var collision = () => ServiceScaffolder.Generate(root, "Invoices", "Sales");
        collision.Should().Throw<InvalidOperationException>();
        RunDotnetTest(Path.Combine(directory, "Tests", "Invoices.Tests.csproj"));
    }

    [Fact]
    public void RequiresParentAndNeverWritesDomainServiceAtRoot()
    {
        var noParent = () => ServiceScaffolder.Generate(root, "Invoices", null);
        noParent.Should().Throw<ArgumentException>();
        Directory.Exists(Path.Combine(root, "src", "Services", "Invoices")).Should().BeFalse();
    }

    [Fact]
    public void SupportsDeepNestingAndValidatorRejectsRootDomainServices()
    {
        var invoices = ServiceScaffolder.Generate(root, "Invoices", "Sales");
        var lines = ServiceScaffolder.Generate(root, "Lines", "Invoices");
        lines.Should().Be(Path.Combine(invoices, "Services", "Lines"));
        ServiceTreeValidator.Validate(Path.Combine(root, "src", "Services")).Should().Contain(node => node.Id == "lines" && node.Parent == "invoices");
        var misplaced = Path.Combine(root, "src", "Services", "Orphans");
        Directory.CreateDirectory(misplaced);
        File.WriteAllText(Path.Combine(misplaced, "orphans.json"), "{\"id\":\"orphans\",\"name\":\"Orphans\",\"version\":\"1\",\"author\":\"test\",\"icon\":\"x\",\"color\":\"x\",\"tags\":[],\"description\":\"x\"}");
        var action = () => ServiceTreeValidator.Validate(Path.Combine(root, "src", "Services"));
        action.Should().Throw<InvalidOperationException>().WithMessage("*requires an owner*");
    }

    [Fact]
    public void ValidatorRejectsPersistentModelsOutsideOwningModelsFolder()
    {
        File.WriteAllText(Path.Combine(root, "src", "Services", "Sales", "BadModel.cs"), "public sealed class BadModel : BaseModel { }");
        var action = () => ServiceTreeValidator.Validate(Path.Combine(root, "src", "Services"));
        action.Should().Throw<InvalidOperationException>().WithMessage("*must live in*Models*");
    }

    [Theory, InlineData(""), InlineData("sales"), InlineData("Bad Name")]
    public void RejectsInvalidNames(string name) => ServiceScaffolder.IsValidName(name).Should().BeFalse();

    public void Dispose() { if (Directory.Exists(root)) Directory.Delete(root, true); }

    private static void RunDotnetTest(string project)
    {
        using var process = Process.Start(new ProcessStartInfo("dotnet", $"test \"{project}\" --nologo") { UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true })!;
        var output = process.StandardOutput.ReadToEnd() + process.StandardError.ReadToEnd();
        process.WaitForExit();
        process.ExitCode.Should().Be(0, output);
    }

    private static string FindRepositoryRoot()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
            if (File.Exists(Path.Combine(directory.FullName, "CompanyHQ.sln"))) return directory.FullName;
        throw new DirectoryNotFoundException();
    }
}
