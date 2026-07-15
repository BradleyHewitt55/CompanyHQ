using System.Diagnostics;
using System.Net;
using System.Text.Json;
using System.Text.RegularExpressions;
using FluentAssertions;

namespace E2E.Tests;

[Trait("Category", "Docker")]
public sealed class HostImageTests
{
    [Fact]
    public async Task SingleImageServesPublicAdminHiddenModulesAndUserService()
    {
        var root = FindRoot();
        var tag = "companyhq-test:" + Guid.NewGuid().ToString("N");
        string? container = null;
        try
        {
            Docker(root, "build", "-f", "docker/Dockerfile", "-t", tag, ".");
            Docker(null, "run", "--rm", "--entrypoint", "sh", tag, "-c", "test $(id -u) -ne 0 && test -f /app/modules/Auth.dll && test -f /app/modules/Postgres.dll && test -f /app/modules/Companies.dll && test -f /app/modules/Orders.dll && test ! -e /source");
            container = Docker(null, "run", "-d", "-P", "--restart", "unless-stopped", "-e", "Database__Provider=sqlite", "-e", "ConnectionStrings__Company=Data Source=/data/company.db", "-e", "Bootstrap__Enabled=true", "-e", "Bootstrap__Token=test-token", tag).Trim();
            var port = Docker(null, "port", container, "8080/tcp").Trim().Split(':')[^1];
            using var client = new HttpClient(new HttpClientHandler { AllowAutoRedirect = false, CookieContainer = new CookieContainer() }) { BaseAddress = new Uri($"http://127.0.0.1:{port}") };
            await Wait(client);
            (await client.GetAsync("/health")).StatusCode.Should().Be(HttpStatusCode.OK);
            (await client.GetStringAsync("/")).Should().Contain("data-layout=\"main\"").And.Contain("data-page=\"public-home\"");

            using var seed = new HttpRequestMessage(HttpMethod.Post, "/internal/bootstrap/owner?username=owner%40docker.test");
            seed.Headers.Add("X-Bootstrap-Token", "test-token");
            var seeded = await client.SendAsync(seed);
            seeded.EnsureSuccessStatusCode();
            var password = JsonDocument.Parse(await seeded.Content.ReadAsStringAsync()).RootElement.GetProperty("password").GetString()!;
            using var seedAgain = new HttpRequestMessage(HttpMethod.Post, "/internal/bootstrap/owner?username=owner%40docker.test");
            seedAgain.Headers.Add("X-Bootstrap-Token", "test-token");
            (await client.SendAsync(seedAgain)).StatusCode.Should().Be(HttpStatusCode.Gone);
            TryDocker("exec", container, "sh", "-c", "kill -9 1");
            await Wait(client);
            using var seedAfterRestart = new HttpRequestMessage(HttpMethod.Post, "/internal/bootstrap/owner?username=owner%40docker.test");
            seedAfterRestart.Headers.Add("X-Bootstrap-Token", "test-token");
            (await client.SendAsync(seedAfterRestart)).StatusCode.Should().Be(HttpStatusCode.Gone);
            var loginHtml = await client.GetStringAsync("/auth/login");
            var token = Regex.Match(loginHtml, "name=\"__RequestVerificationToken\" value=\"([^\"]+)\"").Groups[1].Value;
            var login = await client.PostAsync("/auth/login", new FormUrlEncodedContent(new Dictionary<string, string> { ["Username"] = "owner@docker.test", ["Password"] = password, ["__RequestVerificationToken"] = token }));
            login.StatusCode.Should().Be(HttpStatusCode.Found);
            var authCookie = login.Headers.GetValues("Set-Cookie").Single(value => value.StartsWith("CompanyHQ.Auth=")).Split(';')[0];
            var services = await AuthenticatedHtml(client, "/admin/services", authCookie);
            services.Should().Contain("Orders").And.NotContain("Authentication").And.NotContain("Postgres").And.NotContain("Companies");
            (await AuthenticatedHtml(client, "/admin/services/orders/index", authCookie)).Should().Contain("data-page=\"orders\"");
            using var ordersRequest = new HttpRequestMessage(HttpMethod.Get, "/api/services/orders");
            ordersRequest.Headers.Add("Cookie", authCookie);
            (await client.SendAsync(ordersRequest)).StatusCode.Should().Be(HttpStatusCode.OK);
        }
        finally
        {
            if (container is not null) TryDocker("rm", "-f", container);
            TryDocker("image", "rm", "-f", tag);
        }
    }

    private static async Task<string> AuthenticatedHtml(HttpClient client, string path, string cookie)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, path);
        request.Headers.Add("Cookie", cookie);
        var response = await client.SendAsync(request);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadAsStringAsync();
    }

    private static async Task Wait(HttpClient client)
    {
        for (var attempt = 0; attempt < 120; attempt++)
        {
            try { if ((await client.GetAsync("/")).IsSuccessStatusCode) return; } catch (HttpRequestException) { }
            await Task.Delay(500);
        }
        throw new TimeoutException();
    }

    private static string Docker(string? directory, params string[] arguments)
    {
        using var process = new Process { StartInfo = new ProcessStartInfo("docker") { WorkingDirectory = directory ?? Environment.CurrentDirectory, UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true } };
        foreach (var argument in arguments) process.StartInfo.ArgumentList.Add(argument);
        process.Start();
        var output = process.StandardOutput.ReadToEndAsync();
        var error = process.StandardError.ReadToEndAsync();
        process.WaitForExit();
        Task.WaitAll(output, error);
        if (process.ExitCode != 0) throw new InvalidOperationException(output.Result + Environment.NewLine + error.Result);
        return output.Result;
    }

    private static void TryDocker(params string[] arguments) { try { Docker(null, arguments); } catch { } }
    private static string FindRoot()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
            if (File.Exists(Path.Combine(directory.FullName, "CompanyHQ.sln"))) return directory.FullName;
        throw new DirectoryNotFoundException();
    }
}
