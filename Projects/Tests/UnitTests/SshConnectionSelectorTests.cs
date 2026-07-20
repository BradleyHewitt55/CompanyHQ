using CompanyHQ.Cli;

namespace UnitTests;

public sealed class SshConnectionSelectorTests : IDisposable
{
    private readonly string keys = Path.Combine(Path.GetTempPath(), $"companyhq-keys-{Guid.NewGuid():N}");

    [Fact]
    public void SelectsPasswordWhenNoKeyDirectoryIsGiven()
    {
        var selected = SshConnectionSelector.Select(
            new CliArguments(
                CliCommand.CreateCompany, "Acme", null, "deploy", "server", "secret", null, null));

        Assert.Equal(SshAuthenticationKind.Password, selected.AuthenticationKind);
        Assert.Equal("secret", selected.Credential);
    }

    [Fact]
    public void SelectsPrivateKeyFromConfDirectory()
    {
        Directory.CreateDirectory(keys);
        File.WriteAllText(Path.Combine(keys, "id_ed25519.pub"), "public");
        File.WriteAllText(Path.Combine(keys, "id_ed25519"), "private");

        var selected = SshConnectionSelector.Select(
            new CliArguments(
                CliCommand.CreateCompany, "Acme", null, "deploy", "server", null, keys, null));

        Assert.Equal(SshAuthenticationKind.PrivateKey, selected.AuthenticationKind);
        Assert.EndsWith("id_ed25519", selected.Credential);
    }

    public void Dispose()
    {
        if (Directory.Exists(keys)) Directory.Delete(keys, recursive: true);
    }
}
