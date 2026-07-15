using Cli;
using FluentAssertions;
using Services.Scaffolding;
using Renci.SshNet;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace Cli.Tests;

public sealed class CliTests
{
    [Fact]
    public void NameIsRequiredAndDatabaseDefaultsPostgres()
    {
        new NewCompanySettings().Validate().Successful.Should().BeFalse();
        new NewCompanySettings { Name = "Acme" }.Database.Should().Be("postgres");
        new NewCompanySettings().Output.Should().Be(Path.Combine("CompanyHQ", "Companies"));
    }

    [Fact]
    public void ScaffoldUsesNormalizedImmutableCompanyDirectory()
    {
        var workspace = Path.Combine(Path.GetTempPath(), "companyhq-layout-" + Guid.NewGuid());
        var companies = Path.Combine(workspace, "CompanyHQ", "Companies");
        try
        {
            var result = ScaffoldGenerator.Generate(companies, "Acme Industries");

            result.Directory.Should().Be(Path.Combine(companies, "acme-industries"));
            result.Slug.Should().Be("acme-industries");
            File.ReadAllText(Path.Combine(result.Directory, "company.json")).Should().Contain("\"slug\": \"acme-industries\"");

            var company = JsonNode.Parse(File.ReadAllText(Path.Combine(result.Directory, "company.json")))!.AsObject();
            company["name"] = "Acme Holdings";
            File.WriteAllText(Path.Combine(result.Directory, "company.json"), company.ToJsonString());
            Directory.Exists(Path.Combine(companies, result.Slug)).Should().BeTrue();
            Directory.Exists(Path.Combine(companies, "acme-holdings")).Should().BeFalse();
        }
        finally { if (Directory.Exists(workspace)) Directory.Delete(workspace, true); }
    }

    [Fact]
    public void DocumentedMultiCharacterOptionsAreNormalizedForSpectre()
    {
        CliArguments.Normalize(["new", "-db", "postgres", "-sip", "host", "-conf", "config"])
            .Should().Equal("new", "--database", "postgres", "--server-ip", "host", "--config", "config");
    }

    [Fact]
    public void DatabaseProviderIsResolvedThroughProviderContract()
    {
        DbProviders.Resolve("POSTGRES").Name.Should().Be("postgres");
        var action = () => DbProviders.Resolve("missing");
        action.Should().Throw<NotSupportedException>();
    }

    [Fact]
    public void ScaffoldContainsOwnedSourceExcludesHiddenSourceAndHasSafeConfiguration()
    {
        var output = Path.Combine(Path.GetTempPath(), "companyhq-cli-" + Guid.NewGuid());
        try
        {
            var result = ScaffoldGenerator.Generate(output, "Acme Corp");
            foreach (var path in new[] { "src/Web", "src/Builder", "src/Services", "CompanyHQ/Admin/Pages", "CompanyHQ/Admin/Services", "CompanyHQ/Projects/Website", "lib", "data", "data-protection" }) Directory.Exists(Path.Combine(result.Directory, path)).Should().BeTrue();
            Directory.Exists(Path.Combine(result.Directory, "src/Core")).Should().BeFalse();
            Directory.Exists(Path.Combine(result.Directory, "src", "Services", "Sales", "Services", "Orders")).Should().BeTrue();
            var repository = FindRoot();
            foreach (var service in Directory.EnumerateDirectories(Path.Combine(repository, "src", "Services")))
            {
                var manifestFile = Directory.EnumerateFiles(service, "*.json", SearchOption.TopDirectoryOnly).SingleOrDefault();
                if (manifestFile is null) continue;
                var manifest = Services.Companies.Contracts.Services.ServiceManifest.Parse(File.ReadAllText(manifestFile));
                Directory.Exists(Path.Combine(result.Directory, "src", "Services", Path.GetFileName(service))).Should().Be(!manifest.Hidden);
            }
            Directory.EnumerateFiles(Path.Combine(result.Directory, "lib"), "*.dll").Should().NotBeEmpty();
            foreach (var hidden in new[] { "Auth.dll", "Postgres.dll", "Companies.dll", "Sqlite.dll", "Scaffolding.dll" }) File.Exists(Path.Combine(result.Directory, "lib", hidden)).Should().BeTrue();
            foreach (var hiddenManifest in new[] { "auth.json", "postgres.json", "companies.json", "sqlite.json", "scaffolding.json" }) File.Exists(Path.Combine(result.Directory, "lib", hiddenManifest)).Should().BeTrue();
            foreach (var project in Directory.EnumerateFiles(Path.Combine(result.Directory, "src"), "*.csproj", SearchOption.AllDirectories))
                File.ReadAllText(project).Should().NotMatchRegex("ProjectReference[^>]+(?:Companies|Auth|Postgres|Sqlite|Scaffolding)\\\\[^>]+csproj");
            var company = File.ReadAllText(Path.Combine(result.Directory, "company.json"));
            company.Should().Contain("Acme Corp").And.NotContain(result.DbPassword).And.NotContain(result.BootstrapToken);
            var compose = File.ReadAllText(Path.Combine(result.Directory, "docker-compose.yml"));
            compose.Should().NotContain(result.DbPassword).And.NotContain(result.BootstrapToken).And.Contain("postgres:17-alpine@sha256:").And.Contain("data-protection").And.Contain("target: builder").And.Contain("RuntimeBuilder__Url");
            File.ReadAllText(Path.Combine(result.Directory, ".env")).Should().Contain(result.DbPassword).And.Contain(result.BootstrapToken).And.Contain("COMPANYHQ_BUILDER_TOKEN=");
            result.DbPassword.Should().NotBeNullOrWhiteSpace().And.NotBe("password");
        }
        finally { Directory.Delete(output, true); }
    }

    [Fact]
    public void TwoCompanyScaffoldsShareOnePrivateNetworkAndServerProxy()
    {
        var output = Path.Combine(Path.GetTempPath(), "companyhq-isolation-" + Guid.NewGuid());
        try
        {
            var a = ScaffoldGenerator.Generate(output, "Company A");
            File.AppendAllText(Path.Combine(a.ServerDirectory, "docker-compose.yml"), "# server-owned service\n");
            var b = ScaffoldGenerator.Generate(output, "Company B");
            a.Directory.Should().NotBe(b.Directory);
            var aCompose = File.ReadAllText(Path.Combine(a.Directory, "docker-compose.yml"));
            var bCompose = File.ReadAllText(Path.Combine(b.Directory, "docker-compose.yml"));
            var serverCompose = File.ReadAllText(Path.Combine(a.ServerDirectory, "docker-compose.yml"));
            serverCompose.Should().Contain("80:80").And.Contain("443:443").And.Contain("proxy:").And.Contain("# server-owned service");
            aCompose.Should().NotContain("ports:").And.Contain("external: true").And.Contain("companyhq").And.Contain("company-a-app").And.NotContain(b.Directory);
            bCompose.Should().NotContain("ports:").And.Contain("external: true").And.Contain("companyhq").And.Contain("company-b-app").And.NotContain(a.Directory);
            File.ReadAllText(Path.Combine(a.ServerDirectory, "routes", "company-a.caddy")).Should().Contain("company-a-app:8080");
            File.ReadAllText(Path.Combine(a.ServerDirectory, "routes", "company-b.caddy")).Should().Contain("company-b-app:8080");
            aCompose.Should().Contain("./CompanyHQ/Admin/Services");
            bCompose.Should().Contain("./CompanyHQ/Admin/Services");
            Directory.GetFiles(output, "company.json", SearchOption.AllDirectories).Should().OnlyContain(path => Path.GetDirectoryName(path)!.StartsWith(output, StringComparison.OrdinalIgnoreCase));
            AssertMountsStayWithinCompanyDirectory(aCompose, a.Directory);
            AssertMountsStayWithinCompanyDirectory(bCompose, b.Directory);
        }
        finally { if (Directory.Exists(output)) Directory.Delete(output, true); }
    }

    private static string FindRoot()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
            if (File.Exists(Path.Combine(directory.FullName, "CompanyHQ.sln"))) return directory.FullName;
        throw new DirectoryNotFoundException();
    }

    private static void AssertMountsStayWithinCompanyDirectory(string compose, string companyDirectory)
    {
        var mountPaths = Regex.Matches(compose, @"(?m)^\s*-\s+(\./[^:\r\n]+):")
            .Select(match => match.Groups[1].Value);
        mountPaths.Should().NotBeEmpty().And.OnlyContain(path =>
            Path.GetFullPath(Path.Combine(companyDirectory, path.Substring(2)))
                .StartsWith(Path.GetFullPath(companyDirectory) + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void SshFactoryUsesKeyWhenConfigIsSuppliedAndPasswordOtherwise()
    {
        var config = Path.Combine(Path.GetTempPath(), "companyhq-ssh-" + Guid.NewGuid());
        Directory.CreateDirectory(config);
        try
        {
            using var rsa = System.Security.Cryptography.RSA.Create(2048);
            File.WriteAllText(Path.Combine(config, "id_rsa"), rsa.ExportRSAPrivateKeyPem());
            SshConnectionFactory.Create("host", "user", config, null).AuthenticationMethods.Should().ContainSingle().Which.Should().BeOfType<PrivateKeyAuthenticationMethod>();
            SshConnectionFactory.Create("host", "user", null, "secret").AuthenticationMethods.Should().ContainSingle().Which.Should().BeOfType<PasswordAuthenticationMethod>();
        }
        finally { Directory.Delete(config, true); }
    }
}
