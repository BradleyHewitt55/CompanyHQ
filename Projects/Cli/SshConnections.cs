using Renci.SshNet;

namespace CompanyHQ.Cli;

public enum SshAuthenticationKind
{
    Password,
    PrivateKey,
}

public sealed record SshConnectionSelection(
    string Host,
    string Username,
    SshAuthenticationKind AuthenticationKind,
    string Credential);

public static class SshConnectionSelector
{
    public static SshConnectionSelection Select(CliArguments arguments)
    {
        if (arguments.ServerIp is null || arguments.SshUsername is null)
        {
            throw new ArgumentException("SSH selection requires a server IP and username.");
        }

        if (arguments.KeyDirectory is not null)
        {
            if (!Directory.Exists(arguments.KeyDirectory))
            {
                throw new DirectoryNotFoundException(
                    $"SSH key directory '{arguments.KeyDirectory}' does not exist.");
            }

            var key = Directory.EnumerateFiles(arguments.KeyDirectory)
                .Order(StringComparer.Ordinal)
                .FirstOrDefault(path => !path.EndsWith(".pub", StringComparison.OrdinalIgnoreCase))
                ?? throw new FileNotFoundException(
                    $"No private key file was found in '{arguments.KeyDirectory}'.");
            return new(arguments.ServerIp, arguments.SshUsername, SshAuthenticationKind.PrivateKey, key);
        }

        if (string.IsNullOrEmpty(arguments.Password))
        {
            throw new ArgumentException("SSH password is required when '--conf' is not supplied.");
        }

        return new(arguments.ServerIp, arguments.SshUsername, SshAuthenticationKind.Password, arguments.Password);
    }
}

public interface ISshConnectionFactory
{
    ISshConnection Create(SshConnectionSelection selection);
}

public interface ISshConnection
{
    Task RunAsync(string companyName, CancellationToken cancellationToken = default);
}

public sealed class SshConnectionFactory : ISshConnectionFactory
{
    public ISshConnection Create(SshConnectionSelection selection) => new SshConnection(selection);
}

internal sealed class SshConnection(SshConnectionSelection selection) : ISshConnection
{
    public async Task RunAsync(string companyName, CancellationToken cancellationToken = default)
    {
        var authentication = selection.AuthenticationKind == SshAuthenticationKind.PrivateKey
            ? (AuthenticationMethod)new PrivateKeyAuthenticationMethod(
                selection.Username, new PrivateKeyFile(selection.Credential))
            : new PasswordAuthenticationMethod(selection.Username, selection.Credential);
        var connectionInfo = new ConnectionInfo(selection.Host, selection.Username, authentication);
        using var client = new SshClient(connectionInfo);
        client.Connect();
        using var shell = client.CreateShellStream("companyhq", 80, 24, 800, 600, 4096);

        var escapedName = companyName.Replace("'", "'\"'\"'", StringComparison.Ordinal);
        shell.WriteLine($"companyhq --c '{escapedName}'; exit");

        var output = shell.CopyToAsync(Console.OpenStandardOutput(), cancellationToken);
        var input = Console.OpenStandardInput().CopyToAsync(shell, cancellationToken);
        await Task.WhenAny(output, input);
        if (client.IsConnected)
        {
            client.Disconnect();
        }
    }
}
