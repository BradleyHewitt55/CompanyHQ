using System.Diagnostics;
using System.Net.Http.Json;
using Spectre.Console;
using Spectre.Console.Cli;
using Renci.SshNet;
using Services.Scaffolding;

namespace Cli;

public sealed class NewCompanyCommand : AsyncCommand<NewCompanySettings>
{
    protected override async Task<int> ExecuteAsync(CommandContext context, NewCompanySettings settings, CancellationToken cancellationToken)
    {
        var databaseProvider = DbProviders.Resolve(settings.Database);
        var scaffold = ScaffoldGenerator.Generate(settings.Output, settings.Name, databaseProvider.Name);
        ConnectionInfo? remoteConnection = null;
        if (settings.ServerIp is not null)
        {
            var sshPassword = settings.ConfigDirectory is null ? AnsiConsole.Prompt(new TextPrompt<string>("SSH password:").Secret()) : null;
            remoteConnection = SshConnectionFactory.Create(settings.ServerIp, Environment.UserName, settings.ConfigDirectory, sshPassword);
            UploadAndStart(remoteConnection, scaffold);
        }
        else if (!settings.NoStart)
        {
            await Run("docker", scaffold.Directory, cancellationToken, "compose", "up", "-d");
        }
        if (!settings.NoStart)
        {
            var host = settings.ServerIp ?? "localhost";
            using var client = new HttpClient(new HttpClientHandler { ServerCertificateCustomValidationCallback = HttpClientHandler.DangerousAcceptAnyServerCertificateValidator }) { BaseAddress = new Uri($"https://{host}:{scaffold.HttpsPort}") };
            var ready = false;
            for (var attempt = 0; attempt < 90; attempt++)
            {
                try
                {
                    if ((await client.GetAsync("/", cancellationToken)).IsSuccessStatusCode)
                    {
                        ready = true;
                        break;
                    }
                }
                catch (HttpRequestException) { }
                await Task.Delay(1000, cancellationToken);
            }
            if (!ready) throw new TimeoutException($"Company host at {client.BaseAddress} did not become healthy within 90 seconds.");
            if (settings.Username is not null)
            {
                using var request = new HttpRequestMessage(HttpMethod.Post, $"/internal/bootstrap/owner?username={Uri.EscapeDataString(settings.Username)}");
                request.Headers.Add("X-Bootstrap-Token", scaffold.BootstrapToken);
                var response = await client.SendAsync(request, cancellationToken);
                response.EnsureSuccessStatusCode();
                var owner = await response.Content.ReadFromJsonAsync<SeedResponse>(cancellationToken);
                DisableBootstrap(scaffold, remoteConnection);
                AnsiConsole.MarkupLine($"Admin URL: [link]https://localhost:{scaffold.HttpsPort}/admin[/]");
                AnsiConsole.WriteLine($"Username: {settings.Username}");
                AnsiConsole.WriteLine($"Password: {owner!.Password}");
            }
            var rootUrl = Environment.GetEnvironmentVariable("COMPANYHQ_ROOT_URL");
            var rootToken = Environment.GetEnvironmentVariable("COMPANYHQ_ROOT_TOKEN");
            if (rootUrl is not null && rootToken is not null)
            {
                using var root = new HttpClient { BaseAddress = new Uri(rootUrl) };
                using var registration = new HttpRequestMessage(HttpMethod.Post, "/api/companies")
                {
                    Content = JsonContent.Create(new { name = settings.Name, slug = scaffold.Slug, host, adminUrl = $"https://{host}:{scaffold.HttpsPort}/admin", dbProvider = settings.Database, databaseName = "companyhq", deploymentStatus = "running", createdAt = DateTimeOffset.UtcNow })
                };
                registration.Headers.Add("X-CompanyHQ-Token", rootToken);
                (await root.SendAsync(registration, cancellationToken)).EnsureSuccessStatusCode();
            }
        }
        AnsiConsole.MarkupLine($"Created [green]{settings.Name}[/] at {scaffold.Directory}");
        return 0;
    }

    private static void DisableBootstrap(ScaffoldResult scaffold, ConnectionInfo? remoteConnection)
    {
        var envPath = Path.Combine(scaffold.Directory, ".env");
        File.WriteAllText(envPath, File.ReadAllText(envPath).Replace("COMPANYHQ_BOOTSTRAP_ENABLED=true", "COMPANYHQ_BOOTSTRAP_ENABLED=false", StringComparison.Ordinal));
        if (remoteConnection is null) return;
        using var ssh = new SshClient(remoteConnection);
        ssh.Connect();
        var remoteRoot = $"CompanyHQ/Companies/{scaffold.Slug}".Replace("'", "'\\''");
        var command = ssh.RunCommand($"sed -i 's/COMPANYHQ_BOOTSTRAP_ENABLED=true/COMPANYHQ_BOOTSTRAP_ENABLED=false/' '{remoteRoot}/.env'");
        if (command.ExitStatus != 0) throw new InvalidOperationException(command.Error);
        ssh.Disconnect();
    }

    private static void UploadAndStart(ConnectionInfo connection, ScaffoldResult scaffold)
    {
        var remoteRoot = $"CompanyHQ/Companies/{scaffold.Slug}";
        using (var sftp = new SftpClient(connection))
        {
            sftp.Connect();
            EnsureDirectory(sftp, remoteRoot);
            foreach (var directory in Directory.EnumerateDirectories(scaffold.Directory, "*", SearchOption.AllDirectories)) EnsureDirectory(sftp, remoteRoot + "/" + Path.GetRelativePath(scaffold.Directory, directory).Replace('\\', '/'));
            foreach (var file in Directory.EnumerateFiles(scaffold.Directory, "*", SearchOption.AllDirectories))
            {
                using var stream = File.OpenRead(file);
                sftp.UploadFile(stream, remoteRoot + "/" + Path.GetRelativePath(scaffold.Directory, file).Replace('\\', '/'), true);
            }
            sftp.Disconnect();
        }
        using var ssh = new SshClient(connection);
        ssh.Connect();
        var command = ssh.RunCommand($"cd '{remoteRoot.Replace("'", "'\\''")}' && docker compose up -d");
        if (command.ExitStatus != 0) throw new InvalidOperationException(command.Error);
        ssh.Disconnect();
    }

    private static void EnsureDirectory(SftpClient client, string path)
    {
        var current = string.Empty;
        foreach (var part in path.Split('/', StringSplitOptions.RemoveEmptyEntries))
        {
            current += "/" + part;
            if (!client.Exists(current)) client.CreateDirectory(current);
        }
    }

    private static async Task Run(string file, string directory, CancellationToken cancellationToken, params string[] arguments)
    {
        using var process = new Process { StartInfo = new ProcessStartInfo(file) { WorkingDirectory = directory, UseShellExecute = false } };
        foreach (var argument in arguments) process.StartInfo.ArgumentList.Add(argument);
        process.Start(); await process.WaitForExitAsync(cancellationToken);
        if (process.ExitCode != 0) throw new InvalidOperationException($"{file} exited with code {process.ExitCode}.");
    }
    private sealed record SeedResponse(string Username, string Password, bool Created);
}
