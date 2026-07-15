using System.Diagnostics;
using System.Net.Http.Json;
using System.Text.Json;
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
            await Run("docker", scaffold.ServerDirectory, cancellationToken, "compose", "up", "-d");
            await Run("docker", scaffold.Directory, cancellationToken, "compose", "up", "-d");
        }
        if (!settings.NoStart)
        {
            await WaitForHealthyApp(scaffold, remoteConnection, cancellationToken);
            await ReloadProxy(scaffold, remoteConnection, cancellationToken);
            if (settings.Username is not null)
            {
                var owner = await BootstrapOwner(scaffold, remoteConnection, settings.Username, cancellationToken);
                DisableBootstrap(scaffold, remoteConnection);
                AnsiConsole.MarkupLine($"Admin URL: [link]{scaffold.AdminUrl}[/]");
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
                    Content = JsonContent.Create(new { name = settings.Name, slug = scaffold.Slug, host = $"{scaffold.Slug}.localhost", adminUrl = scaffold.AdminUrl, dbProvider = settings.Database, databaseName = "companyhq", deploymentStatus = "running", createdAt = DateTimeOffset.UtcNow })
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
        const string remoteRoot = "CompanyHQ";
        var remoteCompanyRoot = $"{remoteRoot}/Companies/{scaffold.Slug}";
        using (var sftp = new SftpClient(connection))
        {
            sftp.Connect();
            EnsureDirectory(sftp, remoteRoot);
            EnsureDirectory(sftp, remoteRoot + "/routes");
            if (!sftp.Exists(remoteRoot + "/docker-compose.yml"))
                UploadFile(sftp, Path.Combine(scaffold.ServerDirectory, "docker-compose.yml"), remoteRoot + "/docker-compose.yml");
            if (!sftp.Exists(remoteRoot + "/Caddyfile"))
                UploadFile(sftp, Path.Combine(scaffold.ServerDirectory, "Caddyfile"), remoteRoot + "/Caddyfile");
            UploadFile(sftp, Path.Combine(scaffold.ServerDirectory, "routes", scaffold.Slug + ".caddy"), remoteRoot + "/routes/" + scaffold.Slug + ".caddy");
            foreach (var directory in Directory.EnumerateDirectories(scaffold.Directory, "*", SearchOption.AllDirectories)) EnsureDirectory(sftp, remoteCompanyRoot + "/" + Path.GetRelativePath(scaffold.Directory, directory).Replace('\\', '/'));
            foreach (var file in Directory.EnumerateFiles(scaffold.Directory, "*", SearchOption.AllDirectories))
                UploadFile(sftp, file, remoteCompanyRoot + "/" + Path.GetRelativePath(scaffold.Directory, file).Replace('\\', '/'));
            sftp.Disconnect();
        }
        using var ssh = new SshClient(connection);
        ssh.Connect();
        var command = ssh.RunCommand($"cd '{remoteRoot}' && docker compose up -d && cd 'Companies/{scaffold.Slug}' && docker compose up -d");
        if (command.ExitStatus != 0) throw new InvalidOperationException(command.Error);
        ssh.Disconnect();
    }

    private static void UploadFile(SftpClient client, string source, string destination)
    {
        using var stream = File.OpenRead(source);
        client.UploadFile(stream, destination, true);
    }

    private static async Task WaitForHealthyApp(ScaffoldResult scaffold, ConnectionInfo? remoteConnection, CancellationToken cancellationToken)
    {
        for (var attempt = 0; attempt < 90; attempt++)
        {
            var status = remoteConnection is null
                ? await RunOutput("docker", scaffold.Directory, cancellationToken, "compose", "ps", "-q", "app")
                : RunRemote(remoteConnection, $"cd 'CompanyHQ/Companies/{scaffold.Slug}' && docker compose ps -q app");
            var container = status.Trim();
            if (!string.IsNullOrEmpty(container))
            {
                var health = remoteConnection is null
                    ? await RunOutput("docker", scaffold.Directory, cancellationToken, "inspect", "--format", "{{.State.Health.Status}}", container)
                    : RunRemote(remoteConnection, $"docker inspect --format '{{{{.State.Health.Status}}}}' {container}");
                if (health.Trim() == "healthy") return;
            }
            await Task.Delay(1000, cancellationToken);
        }
        throw new TimeoutException("Company application container did not become healthy within 90 seconds.");
    }

    private static async Task<SeedResponse> BootstrapOwner(ScaffoldResult scaffold, ConnectionInfo? remoteConnection, string username, CancellationToken cancellationToken)
    {
        var arguments = new[] { "compose", "exec", "-T", "app", "curl", "-fsS", "-X", "POST", "-H", $"X-Bootstrap-Token: {scaffold.BootstrapToken}", $"http://localhost:8080/internal/bootstrap/owner?username={Uri.EscapeDataString(username)}" };
        var output = remoteConnection is null
            ? await RunOutput("docker", scaffold.Directory, cancellationToken, arguments)
            : RunRemote(remoteConnection, $"cd 'CompanyHQ/Companies/{scaffold.Slug}' && docker {string.Join(' ', arguments.Select(EscapeShell))}");
        return JsonSerializer.Deserialize<SeedResponse>(output) ?? throw new InvalidOperationException("Owner bootstrap returned no response.");
    }

    private static async Task ReloadProxy(ScaffoldResult scaffold, ConnectionInfo? remoteConnection, CancellationToken cancellationToken)
    {
        if (remoteConnection is null)
        {
            await Run("docker", scaffold.ServerDirectory, cancellationToken, "compose", "exec", "-T", "proxy", "caddy", "reload", "--config", "/etc/caddy/Caddyfile");
            return;
        }
        RunRemote(remoteConnection, "cd 'CompanyHQ' && docker compose exec -T proxy caddy reload --config /etc/caddy/Caddyfile");
    }

    private static string RunRemote(ConnectionInfo connection, string command)
    {
        using var ssh = new SshClient(connection);
        ssh.Connect();
        var result = ssh.RunCommand(command);
        ssh.Disconnect();
        if (result.ExitStatus != 0) throw new InvalidOperationException(result.Error);
        return result.Result;
    }

    private static string EscapeShell(string value) => "'" + value.Replace("'", "'\\''") + "'";

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
    private static async Task<string> RunOutput(string file, string directory, CancellationToken cancellationToken, params string[] arguments)
    {
        using var process = new Process { StartInfo = new ProcessStartInfo(file) { WorkingDirectory = directory, UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true } };
        foreach (var argument in arguments) process.StartInfo.ArgumentList.Add(argument);
        process.Start();
        var output = await process.StandardOutput.ReadToEndAsync(cancellationToken);
        var error = await process.StandardError.ReadToEndAsync(cancellationToken);
        await process.WaitForExitAsync(cancellationToken);
        if (process.ExitCode != 0) throw new InvalidOperationException(error);
        return output;
    }
    private sealed record SeedResponse(string Username, string Password, bool Created);
}
