using System.Diagnostics;
using System.Net;
using System.Text.Json;
using FluentAssertions;
using Microsoft.Playwright;
using Services.Scaffolding;

namespace E2E.Tests;

[Trait("Category", "Docker")]
public sealed class BrowserAcceptanceTests
{
    [Fact]
    public async Task ExplorerAddsAndLoadsServiceThroughBuilderSidecarWithoutRestart()
    {
        var output = Path.Combine(Path.GetTempPath(), "companyhq-browser-compose-" + Guid.NewGuid());
        Directory.CreateDirectory(output);
        string? directory = null;
        try
        {
            var scaffold = ScaffoldGenerator.Generate(output, "Browser Compose");
            directory = scaffold.Directory;
            Docker(scaffold.ServerDirectory, "compose", "up", "-d");
            Docker(directory, "compose", "up", "-d");
            var url = scaffold.AdminUrl.Replace("/admin", "", StringComparison.Ordinal);
            using var client = new HttpClient(new HttpClientHandler { ServerCertificateCustomValidationCallback = HttpClientHandler.DangerousAcceptAnyServerCertificateValidator }) { BaseAddress = new Uri(url) };
            for (var attempt = 0; attempt < 120; attempt++)
            {
                try { if ((await client.GetAsync("/health")).IsSuccessStatusCode) break; } catch (HttpRequestException) { }
                if (attempt == 119) throw new TimeoutException();
                await Task.Delay(500);
            }
            var seeded = Docker(directory, "compose", "exec", "-T", "app", "curl", "-fsS", "-X", "POST", "-H", $"X-Bootstrap-Token: {scaffold.BootstrapToken}", "http://localhost:8080/internal/bootstrap/owner?username=admin");
            var password = JsonDocument.Parse(seeded).RootElement.GetProperty("password").GetString()!;
            var appBefore = Docker(directory, "compose", "ps", "-q", "app").Trim();

            using var playwright = await Playwright.CreateAsync();
            await using var browser = await playwright.Chromium.LaunchAsync(new() { Headless = true });
            var context = await browser.NewContextAsync(new() { IgnoreHTTPSErrors = true, ViewportSize = new() { Width = 1440, Height = 900 } });
            var page = await context.NewPageAsync();
            await Login(page, url, password);
            await page.WaitForTimeoutAsync(2000);
            var add = page.Locator("button[aria-label='Add Admin service']");
            (await add.CountAsync()).Should().Be(1, await page.ContentAsync());
            await add.ClickAsync(new() { Force = true });
            await page.GetByRole(AriaRole.Button, new() { Name = "Blank service" }).ClickAsync();
            await page.Locator(".service-tree-node[data-service=blank-service]").WaitForAsync(new() { Timeout = 60000 });
            await page.GotoAsync(url + "/admin/services");
            (await page.Locator("article.service-card").Last.GetAttributeAsync("data-service")).Should().Be("blank-service");
            await page.GotoAsync(url + "/admin/services/blank-service/index");
            (await page.Locator("[data-page=blank-service]").CountAsync()).Should().Be(1);
            Docker(directory, "compose", "ps", "-q", "app").Trim().Should().Be(appBefore);
        }
        finally
        {
            if (directory is not null) TryDockerIn(directory, "compose", "down", "-v");
            if (directory is not null) TryDockerIn(Directory.GetParent(directory)!.FullName, "compose", "down", "-v");
            if (Directory.Exists(output)) Directory.Delete(output, true);
        }
    }

    [Fact]
    public async Task TwoCompaniesRenderPersistOrdersAndRejectTheOtherCompanyCookie()
    {
        var root = FindRoot();
        var tag = "companyhq-browser:" + Guid.NewGuid().ToString("N");
        var containers = new List<string>();
        var overrideRoot = Path.Combine(Path.GetTempPath(), "companyhq-browser-overrides-" + Guid.NewGuid());
        var aPages = Path.Combine(overrideRoot, "a"); var bPages = Path.Combine(overrideRoot, "b");
        Directory.CreateDirectory(aPages); Directory.CreateDirectory(bPages);
        await File.WriteAllTextAsync(Path.Combine(aPages, "users.html"), "<h1>company-a-users-marker</h1>");
        await File.WriteAllTextAsync(Path.Combine(bPages, "users.html"), "<h1>company-b-users-marker</h1>");
        try
        {
            Docker(root, "build", "-f", "docker/Dockerfile", "-t", tag, ".");
            var a = await StartCompany(tag, "browser-a", containers, aPages);
            var b = await StartCompany(tag, "browser-b", containers, bPages);
            using var playwright = await Playwright.CreateAsync();
            await using var browser = await playwright.Chromium.LaunchAsync(new() { Headless = true });
            var context = await browser.NewContextAsync(new() { ViewportSize = new() { Width = 1440, Height = 900 } });
            var page = await context.NewPageAsync();
            var browserErrors = new List<string>();
            page.PageError += (_, error) => browserErrors.Add(error);
            page.Console += (_, message) => browserErrors.Add($"console {message.Type}: {message.Text}");
            page.RequestFailed += (_, request) => browserErrors.Add($"failed {request.Url}: {request.Failure}");

            await Login(page, a.Url, a.Password);
            await page.GotoAsync(a.Url + "/admin/dashboard");
            (await page.Locator("[data-page=dashboard]").CountAsync()).Should().Be(1);
            (await page.Locator(".file-explorer").InnerTextAsync()).Should().Contain("Projects").And.Contain("Website").And.NotContain("Postgres");
            (await page.Locator(".service-tree-node[data-service=sales]").InnerTextAsync()).Should().Contain("Orders").And.Contain("Quotes").And.Contain("Products");
            await page.GotoAsync(a.Url + "/admin/services");
            await page.WaitForTimeoutAsync(2000);
            var salesCard = page.Locator("article[data-service=sales]");
            await salesCard.GetByRole(AriaRole.Button, new() { Name = "Toggle Sales" }).ClickAsync();
            await salesCard.GetByText("Disabled").WaitForAsync();
            page.Url.Should().Be(a.Url + "/admin/services");
            await salesCard.GetByRole(AriaRole.Button, new() { Name = "Toggle Sales" }).ClickAsync();
            await salesCard.GetByText("Enabled").WaitForAsync();
            await page.GotoAsync(a.Url + "/admin/users");
            (await page.GetByText("company-a-users-marker").CountAsync()).Should().Be(1);
            File.Delete(Path.Combine(aPages, "users.html"));
            await page.ReloadAsync();
            (await page.Locator("[data-page=users]").CountAsync()).Should().Be(1);
            await page.GotoAsync(a.Url + "/admin/services/orders/index");
            await page.WaitForTimeoutAsync(3000);
            browserErrors.Add("markers=" + await page.EvaluateAsync<bool>("() => document.documentElement.innerHTML.includes('blazor:')"));
            browserErrors.Add("blazor=" + await page.EvaluateAsync<string>("() => typeof window.Blazor"));
            await page.GetByLabel("Order reference").FillAsync("browser-order-a");
            await page.GetByLabel("Order total").FillAsync("19.95");
            await page.GetByRole(AriaRole.Button, new() { Name = "Create order" }).ClickAsync();
            await Task.Delay(2000);
            if (await page.GetByText("browser-order-a").CountAsync() == 0)
                throw new InvalidOperationException(string.Join(Environment.NewLine, browserErrors) + Environment.NewLine + Docker(null, "logs", "--tail", "100", a.Container));
            await page.ReloadAsync();
            (await page.GetByText("browser-order-a").CountAsync()).Should().Be(1);

            await page.GotoAsync(b.Url + "/admin/dashboard");
            page.Url.Should().Contain("/auth/login");
            await Login(page, b.Url, b.Password);
            (await page.Locator("[data-page=dashboard]").CountAsync()).Should().Be(1);
            (await page.Locator(".file-explorer").InnerTextAsync()).Should().Contain("Projects").And.NotContain("Postgres");
            await page.GotoAsync(b.Url + "/admin/users");
            (await page.GetByText("company-b-users-marker").CountAsync()).Should().Be(1);
            await page.GotoAsync(b.Url + "/admin/services/orders/index");
            await page.WaitForTimeoutAsync(3000);
            (await page.GetByText("browser-order-a").CountAsync()).Should().Be(0);
            await page.GetByLabel("Order reference").FillAsync("browser-order-b");
            await page.GetByLabel("Order total").FillAsync("7.50");
            await page.GetByRole(AriaRole.Button, new() { Name = "Create order" }).ClickAsync();
            await page.GetByText("browser-order-b").WaitForAsync();
        }
        finally
        {
            foreach (var container in containers) TryDocker("rm", "-f", container);
            TryDocker("image", "rm", "-f", tag);
            if (Directory.Exists(overrideRoot)) Directory.Delete(overrideRoot, true);
        }
    }

    private static async Task<(string Url, string Password, string Container)> StartCompany(string tag, string companyId, List<string> containers, string pages)
    {
        var container = Docker(null, "run", "-d", "-P", "-v", $"{pages}:/app/company-pages", "-e", "AdminPagesPath=/app/company-pages", "-e", "Database__Provider=sqlite", "-e", "ConnectionStrings__Company=Data Source=/data/company.db", "-e", $"Company__Id={companyId}", "-e", "Bootstrap__Enabled=true", "-e", "Bootstrap__Token=test-token", tag).Trim();
        containers.Add(container);
        var port = Docker(null, "port", container, "8080/tcp").Trim().Split(':')[^1];
        var url = $"http://localhost:{port}";
        using var client = new HttpClient { BaseAddress = new Uri(url) };
        for (var attempt = 0; attempt < 120; attempt++)
        {
            try { if ((await client.GetAsync("/health")).IsSuccessStatusCode) break; } catch (HttpRequestException) { }
            if (attempt == 119) throw new TimeoutException($"{companyId} did not become healthy.");
            await Task.Delay(500);
        }
        using var seed = new HttpRequestMessage(HttpMethod.Post, "/internal/bootstrap/owner?username=admin");
        seed.Headers.Add("X-Bootstrap-Token", "test-token");
        var response = await client.SendAsync(seed);
        response.EnsureSuccessStatusCode();
        return (url, JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement.GetProperty("password").GetString()!, container);
    }

    private static async Task Login(IPage page, string url, string password)
    {
        await page.GotoAsync(url + "/auth/login");
        await page.GetByLabel("Username").FillAsync("admin");
        await page.GetByLabel("Password").FillAsync(password);
        await page.GetByRole(AriaRole.Button, new() { Name = "Sign in" }).ClickAsync();
        await page.WaitForURLAsync("**/admin/dashboard");
    }

    private static string Docker(string? directory, params string[] arguments)
    {
        using var process = new Process { StartInfo = new ProcessStartInfo("docker") { WorkingDirectory = directory ?? Environment.CurrentDirectory, UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true } };
        foreach (var argument in arguments) process.StartInfo.ArgumentList.Add(argument);
        process.Start();
        var output = process.StandardOutput.ReadToEndAsync(); var error = process.StandardError.ReadToEndAsync();
        process.WaitForExit(); Task.WaitAll(output, error);
        if (process.ExitCode != 0) throw new InvalidOperationException(output.Result + Environment.NewLine + error.Result);
        return output.Result;
    }

    private static void TryDocker(params string[] arguments) { try { Docker(null, arguments); } catch { } }
    private static void TryDockerIn(string directory, params string[] arguments) { try { Docker(directory, arguments); } catch { } }
    private static string FindRoot()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
            if (File.Exists(Path.Combine(directory.FullName, "CompanyHQ.sln"))) return directory.FullName;
        throw new DirectoryNotFoundException();
    }
}
