using System.Diagnostics;
using System.Net;
using System.Text.Json;
using System.Text.RegularExpressions;
using FluentAssertions;
using Microsoft.Data.Sqlite;

namespace Cli.Tests;

[Trait("Category", "Docker")]
public sealed class LocalProvisioningTests
{
    [Fact]
    public async Task NewCommandProvisionsTwoCompaniesLoginAndRegistersWithRoot()
    {
        var root = FindRoot();
        var output = Path.Combine(Path.GetTempPath(), "companyhq-provision-" + Guid.NewGuid());
        var registry = Path.Combine(Path.GetTempPath(), "companyhq-root-" + Guid.NewGuid());
        Directory.CreateDirectory(output); Directory.CreateDirectory(registry);
        var rootPort = Random.Shared.Next(51000, 59000);
        Process? rootProcess = null;
        var companyDirectories = new List<string>();
        try
        {
            await Run("docker", root, ["build", "-f", "docker/Dockerfile", "-t", "companyhq:dev", "."]);
            rootProcess = Start("dotnet", root, ["run", "--project", "src/Web/Web.csproj", "--no-launch-profile", "--", "--urls", $"http://127.0.0.1:{rootPort}"], new()
            {
                ["ConnectionStrings__Registry"] = $"Data Source={Path.Combine(registry, "companies.db")}", ["Registry__Token"] = "root-token"
            });
            using var rootClient = new HttpClient { BaseAddress = new Uri($"http://127.0.0.1:{rootPort}") };
            await Wait(rootClient, "/");
            for (var companyIndex = 0; companyIndex < 2; companyIndex++)
            {
                var name = $"DemoCo{companyIndex}" + Guid.NewGuid().ToString("N")[..6];
                var result = await Run("dotnet", root, ["run", "--project", "src/Cli/Cli.csproj", "--", "new", "-n", name, "-u", "admin", "--output", output], new()
                {
                    ["COMPANYHQ_ROOT_URL"] = rootClient.BaseAddress!.ToString(), ["COMPANYHQ_ROOT_TOKEN"] = "root-token",
                    ["COMPANYHQ_DATA_PATH"] = "company-data"
                });
                var companyDirectory = Directory.GetDirectories(output).Except(companyDirectories).Single();
                companyDirectories.Add(companyDirectory);
                var companyJson = JsonDocument.Parse(File.ReadAllText(Path.Combine(companyDirectory, "company.json")));
                companyJson.RootElement.TryGetProperty("appPort", out _).Should().BeFalse();
                companyJson.RootElement.TryGetProperty("httpsPort", out _).Should().BeFalse();
                var password = Regex.Match(result.Output, "Password: ([^\\r\\n]+)").Groups[1].Value.Trim();
                password.Should().NotBeNullOrWhiteSpace();
                Regex.Matches(result.Output, Regex.Escape(password)).Should().ContainSingle();
                File.ReadAllText(Path.Combine(companyDirectory, "company.json")).Should().NotContain(password);
                File.ReadAllText(Path.Combine(companyDirectory, "docker-compose.yml")).Should().NotContain(password);
                var host = $"{companyJson.RootElement.GetProperty("slug").GetString()}.localhost";
                using var company = new HttpClient(new HttpClientHandler { AllowAutoRedirect = false, ServerCertificateCustomValidationCallback = HttpClientHandler.DangerousAcceptAnyServerCertificateValidator }) { BaseAddress = new Uri($"https://{host}") };
                (await company.GetAsync("/health")).StatusCode.Should().Be(HttpStatusCode.OK);
                File.ReadAllText(Path.Combine(companyDirectory, "docker-compose.yml")).Should().NotContain("ports:");
                var login = await company.GetStringAsync("/auth/login");
                var token = Regex.Match(login, "name=\"__RequestVerificationToken\" value=\"([^\"]+)\"").Groups[1].Value;
                var signedIn = await company.PostAsync("/auth/login", new FormUrlEncodedContent(new Dictionary<string, string> { ["Username"] = "admin", ["Password"] = password, ["__RequestVerificationToken"] = token }));
                signedIn.StatusCode.Should().Be(HttpStatusCode.Found);
            }
            await using (var registryDatabase = new SqliteConnection($"Data Source={Path.Combine(registry, "companies.db")}"))
            {
                await registryDatabase.OpenAsync();
                await using var registered = registryDatabase.CreateCommand();
                registered.CommandText = "SELECT COUNT(*) FROM Companies WHERE Name LIKE 'DemoCo%'";
                Convert.ToInt32(await registered.ExecuteScalarAsync()).Should().Be(2);
            }
            SqliteConnection.ClearAllPools();
        }
        finally
        {
            if (Directory.Exists(output)) foreach (var companyDirectory in Directory.GetDirectories(output)) await Run("docker", companyDirectory, ["compose", "down", "-v"], ignoreFailure: true);
            if (Directory.Exists(output)) await Run("docker", output, ["compose", "down", "-v"], ignoreFailure: true);
            if (rootProcess is not null && !rootProcess.HasExited)
            {
                rootProcess.Kill(true);
                await rootProcess.WaitForExitAsync();
            }
            await DeleteDirectory(output);
            await DeleteDirectory(registry);
        }
    }

    private static async Task Wait(HttpClient client, string path) { for (var i = 0; i < 60; i++) { try { if ((await client.GetAsync(path)).IsSuccessStatusCode) return; } catch { } await Task.Delay(500); } throw new TimeoutException(); }
    private static Process Start(string file, string directory, string[] args, Dictionary<string, string?> environment) { var info = Info(file, directory, args, environment); info.RedirectStandardOutput = false; info.RedirectStandardError = false; var process = new Process { StartInfo = info }; process.Start(); return process; }
    private static async Task<(string Output, string Error)> Run(string file, string directory, string[] args, Dictionary<string, string?>? environment = null, bool ignoreFailure = false) { using var process = new Process { StartInfo = Info(file, directory, args, environment) }; process.Start(); var output = process.StandardOutput.ReadToEndAsync(); var error = process.StandardError.ReadToEndAsync(); await process.WaitForExitAsync(); var stdout = await output; var stderr = await error; if (!ignoreFailure && process.ExitCode != 0) throw new InvalidOperationException(stdout + Environment.NewLine + stderr); return (stdout, stderr); }
    private static ProcessStartInfo Info(string file, string directory, string[] args, Dictionary<string, string?>? environment) { var info = new ProcessStartInfo(file) { WorkingDirectory = directory, UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true }; info.Environment["DOTNET_CLI_DO_NOT_USE_MSBUILD_SERVER"] = "1"; info.Environment["MSBUILDDISABLENODEREUSE"] = "1"; foreach (var arg in args) info.ArgumentList.Add(arg); if (environment is not null) foreach (var pair in environment) info.Environment[pair.Key] = pair.Value; return info; }
    private static string FindRoot() { var directory = new DirectoryInfo(AppContext.BaseDirectory); while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "CompanyHQ.sln"))) directory = directory.Parent; return directory!.FullName; }
    private static async Task DeleteDirectory(string path) { for (var attempt = 0; attempt < 20 && Directory.Exists(path); attempt++) { try { Directory.Delete(path, true); return; } catch (IOException) when (attempt < 19) { await Task.Delay(100); } } }
}
