using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.RegularExpressions;
using CompanyHQ.Services.Companies.Services;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace IntegrationTests;

[Collection(PostgresCollection.Name)]
public sealed class AdminHostTests(PostgresFixture fixture)
{
    [Fact]
    public async Task ClientAuthAndAdminUserCrudUseJsonContracts()
    {
        var company = $"Crud{Guid.NewGuid():N}";
        await SeedCompanyAsync(company, "admin@example.com", "admin-pass");
        await using var factory = new AdminFactory(
            fixture, new Dictionary<string, string> { ["crud.test"] = company });
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://crud.test"),
            AllowAutoRedirect = false,
            HandleCookies = true,
        });
        var registration = new
        {
            name = "Casey",
            surname = "Client",
            email = "CASEY@example.com",
            contactNumber = "+27111",
            password = "client-pass",
            confirmation = "client-pass",
        };

        var registered = await client.PostAsJsonAsync("/api/auth/register", registration);
        Assert.Equal(HttpStatusCode.Created, registered.StatusCode);
        var registeredUser = await registered.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("casey@example.com", registeredUser.GetProperty("email").GetString());
        Assert.Equal("Client", registeredUser.GetProperty("role").GetString());

        var duplicateRegistration = await client.PostAsJsonAsync("/api/auth/register", registration);
        Assert.Equal(HttpStatusCode.Conflict, duplicateRegistration.StatusCode);
        var duplicateRegistrationBody = await duplicateRegistration.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(
            "A user with this email already exists.",
            duplicateRegistrationBody.GetProperty("error").GetString());

        var clientLogin = await client.PostAsJsonAsync("/api/auth/login", new
        {
            email = "casey@example.com",
            password = "client-pass",
        });
        Assert.Equal(HttpStatusCode.OK, clientLogin.StatusCode);
        Assert.Contains(
            clientLogin.Headers.GetValues("Set-Cookie"),
            value => value.StartsWith("CompanyHq.Auth=", StringComparison.Ordinal));
        var clientMe = await client.GetAsync("/api/auth/me");
        Assert.Equal(HttpStatusCode.OK, clientMe.StatusCode);
        var clientMeUser = await clientMe.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("Client", clientMeUser.GetProperty("role").GetString());
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync("/api/users")).StatusCode);

        var adminLogin = await PostLoginAsync(client, "admin@example.com", "admin-pass");
        Assert.Equal(HttpStatusCode.Redirect, adminLogin.StatusCode);
        var currentAdmin = await (await client.GetAsync("/api/auth/me"))
            .Content.ReadFromJsonAsync<JsonElement>();
        var adminId = currentAdmin.GetProperty("id").GetGuid();

        var created = await client.PostAsJsonAsync("/api/users", new
        {
            name = "Crud",
            surname = "Target",
            email = "crud@example.com",
            contactNumber = "+27222",
            role = "Client",
            password = "created-pass",
        });
        Assert.Equal(HttpStatusCode.OK, created.StatusCode);
        var createdUser = await created.Content.ReadFromJsonAsync<JsonElement>();
        var createdId = createdUser.GetProperty("id").GetGuid();

        var listed = await client.GetAsync("/api/users");
        Assert.Equal(HttpStatusCode.OK, listed.StatusCode);
        var listedUsers = await listed.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Contains(
            listedUsers.EnumerateArray(),
            user => user.GetProperty("id").GetGuid() == createdId);

        var fetched = await client.GetAsync($"/api/users/{createdId}");
        Assert.Equal(HttpStatusCode.OK, fetched.StatusCode);
        var fetchedUser = await fetched.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("crud@example.com", fetchedUser.GetProperty("email").GetString());

        var queried = await client.GetAsync("/api/users/query?page=1&pageSize=20&search=CRUD&role=Client");
        Assert.Equal(HttpStatusCode.OK, queried.StatusCode);
        var queryResult = await queried.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(1, queryResult.GetProperty("page").GetInt32());
        Assert.Equal(20, queryResult.GetProperty("pageSize").GetInt32());
        Assert.Equal(1, queryResult.GetProperty("totalCount").GetInt32());
        Assert.Equal(createdId, queryResult.GetProperty("items")[0].GetProperty("id").GetGuid());

        var selfDelete = await client.DeleteAsync($"/api/users/{adminId}");
        Assert.Equal(HttpStatusCode.BadRequest, selfDelete.StatusCode);

        var duplicateSave = await client.PostAsJsonAsync("/api/users", new
        {
            name = "Duplicate",
            surname = "Client",
            email = "casey@example.com",
            contactNumber = "+27333",
            role = "Client",
            password = "duplicate-pass",
        });
        Assert.Equal(HttpStatusCode.Conflict, duplicateSave.StatusCode);
        var duplicateSaveBody = await duplicateSave.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(
            "A user with this email already exists.",
            duplicateSaveBody.GetProperty("error").GetString());

        var deleted = await client.DeleteAsync($"/api/users/{createdId}");
        Assert.Equal(HttpStatusCode.NoContent, deleted.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"/api/users/{createdId}")).StatusCode);
    }

    [Fact]
    public async Task LoginProtectsAdminAndReturnsSafeCurrentUserPayload()
    {
        var company = $"Web{Guid.NewGuid():N}";
        await SeedCompanyAsync(company, "lin@example.com", "secret-pass");
        await using var factory = new AdminFactory(fixture, new Dictionary<string, string> { ["web.test"] = company });
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://web.test"),
            AllowAutoRedirect = false,
            HandleCookies = true,
        });

        var admin = await client.GetAsync("/admin");
        Assert.Equal(HttpStatusCode.Redirect, admin.StatusCode);
        Assert.Equal("/admin/login", admin.Headers.Location?.PathAndQuery);

        var wrong = await PostLoginAsync(client, "lin@example.com", "wrong");
        Assert.Equal(HttpStatusCode.Redirect, wrong.StatusCode);
        var wrongPage = await client.GetAsync(wrong.Headers.Location);
        var wrongHtml = await wrongPage.Content.ReadAsStringAsync();
        Assert.Contains("Invalid email or password", wrongHtml);

        var login = await PostLoginAsync(client, "lin@example.com", "secret-pass");
        Assert.Equal(HttpStatusCode.Redirect, login.StatusCode);
        Assert.Equal("/admin", login.Headers.Location?.ToString());
        var authCookie = Assert.Single(login.Headers.GetValues("Set-Cookie"));
        Assert.StartsWith("CompanyHq.Auth=", authCookie);
        Assert.Contains("httponly", authCookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("secure", authCookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("samesite=lax", authCookie, StringComparison.OrdinalIgnoreCase);

        var me = await client.GetAsync("/api/auth/me");
        Assert.Equal(HttpStatusCode.OK, me.StatusCode);
        using var payload = JsonDocument.Parse(await me.Content.ReadAsStringAsync());
        Assert.NotEqual(Guid.Empty, payload.RootElement.GetProperty("id").GetGuid());
        Assert.Equal("Lin", payload.RootElement.GetProperty("name").GetString());
        Assert.Equal("Tenant", payload.RootElement.GetProperty("surname").GetString());
        Assert.Equal("lin@example.com", payload.RootElement.GetProperty("email").GetString());
        Assert.Equal("+27000", payload.RootElement.GetProperty("contactNumber").GetString());
        Assert.Equal("Admin", payload.RootElement.GetProperty("role").GetString());
        Assert.False(payload.RootElement.TryGetProperty("passwordHash", out _));

    }

    [Fact]
    public async Task CompanyCookieIsRejectedOnAnotherCompanyDomain()
    {
        var companyA = $"TenantA{Guid.NewGuid():N}";
        var companyB = $"TenantB{Guid.NewGuid():N}";
        await SeedCompanyAsync(companyA, "a@example.com", "secret-a");
        await SeedCompanyAsync(companyB, "b@example.com", "secret-b");
        await using var factory = new AdminFactory(fixture, new Dictionary<string, string>
        {
            ["a.test"] = companyA,
            ["b.test"] = companyB,
        });
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://a.test"),
            AllowAutoRedirect = false,
            HandleCookies = true,
        });

        var login = await PostLoginAsync(client, "a@example.com", "secret-a");
        Assert.True(login.StatusCode == HttpStatusCode.Redirect,
            $"Expected login redirect, got {login.StatusCode}: {await login.Content.ReadAsStringAsync()}");
        var setCookie = Assert.Single(login.Headers.GetValues("Set-Cookie"));
        var cookie = setCookie.Split(';', 2)[0];
        using var request = new HttpRequestMessage(HttpMethod.Get, "https://b.test/api/auth/me");
        request.Headers.Add("Cookie", cookie);

        var isolated = await client.SendAsync(request);
        Assert.Equal(HttpStatusCode.Unauthorized, isolated.StatusCode);
    }

    [Fact]
    public async Task RepeatedFailedLoginsAreRateLimited()
    {
        var company = $"Limited{Guid.NewGuid():N}";
        await SeedCompanyAsync(company, "limit@example.com", "secret-pass");
        await using var factory = new AdminFactory(fixture, new Dictionary<string, string> { ["limit.test"] = company });
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://limit.test"),
            AllowAutoRedirect = false,
            HandleCookies = true,
        });

        for (var attempt = 0; attempt < 5; attempt++)
        {
            var allowed = await PostLoginAsync(client, "limit@example.com", "wrong");
            Assert.Equal(HttpStatusCode.Redirect, allowed.StatusCode);
        }

        var throttled = await PostLoginAsync(client, "limit@example.com", "wrong");
        Assert.Equal(HttpStatusCode.TooManyRequests, throttled.StatusCode);
    }

    private async Task SeedCompanyAsync(string company, string email, string password)
    {
        var connectionString = await fixture.CreateExternalDatabaseAsync();
        await using var provider = fixture.CreateProvider();
        var console = new ScriptedConsole(
            ["Lin", "Tenant", email, "+27000", "2", connectionString],
            [password, password]);
        await provider.GetRequiredService<CompanyCreation>().CreateAsync(company, console);
    }

    private static async Task<HttpResponseMessage> PostLoginAsync(
        HttpClient client,
        string email,
        string password)
    {
        var page = await client.GetAsync("/admin/login");
        var html = await page.Content.ReadAsStringAsync();
        Assert.True(page.IsSuccessStatusCode, html);
        var form = HiddenInputs(html);
        form["Email"] = email;
        form["Password"] = password;
        return await client.PostAsync("/api/auth/login", new FormUrlEncodedContent(form));
    }

    private static Dictionary<string, string> HiddenInputs(string html)
    {
        var result = new Dictionary<string, string>();
        foreach (Match input in Regex.Matches(html, "<input[^>]+type=\\\"hidden\\\"[^>]*>", RegexOptions.IgnoreCase))
        {
            var name = Regex.Match(input.Value, "name=\\\"([^\\\"]+)\\\"", RegexOptions.IgnoreCase);
            var value = Regex.Match(input.Value, "value=\\\"([^\\\"]*)\\\"", RegexOptions.IgnoreCase);
            if (name.Success) result[WebUtility.HtmlDecode(name.Groups[1].Value)] = WebUtility.HtmlDecode(value.Groups[1].Value);
        }
        return result;
    }

    private sealed class AdminFactory(PostgresFixture fixture, IReadOnlyDictionary<string, string> domains)
        : WebApplicationFactory<global::Program>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Development");
            builder.ConfigureAppConfiguration((_, configuration) =>
            {
                var settings = new Dictionary<string, string?>
                {
                    ["CompanyHq:ServerRoot"] = fixture.ServerRoot,
                    ["ConnectionStrings:CompanyHqServer"] = PostgresFixture.AdminConnection,
                };
                foreach (var domain in domains)
                {
                    settings[$"CompanyHq:Domains:{domain.Key}"] = domain.Value;
                }
                configuration.AddInMemoryCollection(settings);
            });
        }
    }
}
