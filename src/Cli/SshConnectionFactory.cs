using Renci.SshNet;

namespace Cli;

public static class SshConnectionFactory
{
    public static ConnectionInfo Create(string host, string username, string? configDirectory, string? password)
    {
        if (!string.IsNullOrWhiteSpace(configDirectory))
        {
            var key = Directory.EnumerateFiles(configDirectory, "id_*", SearchOption.TopDirectoryOnly).FirstOrDefault(path => !path.EndsWith(".pub", StringComparison.OrdinalIgnoreCase))
                ?? throw new FileNotFoundException("No SSH private key was found in the config directory.");
            return new ConnectionInfo(host, username, new PrivateKeyAuthenticationMethod(username, new PrivateKeyFile(key)));
        }
        return new ConnectionInfo(host, username, new PasswordAuthenticationMethod(username, password ?? throw new ArgumentNullException(nameof(password))));
    }
}
