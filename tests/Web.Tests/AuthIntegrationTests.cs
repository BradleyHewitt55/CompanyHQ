using System.Net;
using System.Text.Json;
using Services.Postgres;
using Services.Companies.Contracts.Services;
using Services.Auth.Models;
using Services.Auth;
using Services.Auth.Services;
using FluentAssertions;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.Extensions.DependencyInjection.Extensions;
using System.Text.RegularExpressions;
using Npgsql;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.Http.Metadata;

namespace Web.Tests;

[Collection(AuthCollection.Name)]
public sealed class AuthIntegrationTests(AuthHostFixture fixture)
{
    [Fact]
    public async Task SeededOwnerIsIdempotentHashedAndGeneratedPasswordMeetsPolicy()
    {
        PasswordGeneratorService.Generate().Should().MatchRegex("^(?=.*[a-z])(?=.*[A-Z])(?=.*[0-9])(?=.*[^a-zA-Z0-9]).{12,}$");
        await using var scope = fixture.Factory.Services.CreateAsyncScope();
        var second = await scope.ServiceProvider.GetRequiredService<AdminUserSeederService>().SeedOwner(fixture.OwnerUsername);
        second.Created.Should().BeFalse();
        var context = scope.ServiceProvider.GetRequiredService<CompanyDbContext>();
        var user = await context.Users.SingleAsync(item => item.UserName == fixture.OwnerUsername);
        user.PasswordHash.Should().NotBeNull().And.NotContain(fixture.OwnerPassword);
        (await scope.ServiceProvider.GetRequiredService<UserManager<CompanyUser>>().CheckPasswordAsync(user, fixture.OwnerPassword)).Should().BeTrue();
    }

    [Fact]
    public async Task BootstrapUsernameWithoutAtSignGetsAUniqueCapturedEmail()
    {
        var username = "bootstrap-" + Guid.NewGuid().ToString("N");
        await using var scope = fixture.Factory.Services.CreateAsyncScope();
        var seeded = await scope.ServiceProvider.GetRequiredService<AdminUserSeederService>().SeedOwner(username);
        seeded.Created.Should().BeTrue();
        var user = await scope.ServiceProvider.GetRequiredService<UserManager<CompanyUser>>().FindByNameAsync(username);
        user!.Email.Should().StartWith(username + "@").And.EndWith(".companyhq.local");
    }

    [Fact]
    public async Task LoginFailureIsGenericCookieIsHardenedAndExternalReturnUrlIsRejected()
    {
        using var unknown = fixture.CreateClient();
        using var wrong = fixture.CreateClient();
        var unknownResponse = await fixture.Login(unknown, "missing@company.test", "WrongPassword!123");
        var wrongResponse = await fixture.Login(wrong, fixture.OwnerUsername, "WrongPassword!123");
        (await unknownResponse.Content.ReadAsStringAsync()).Should().Be(await wrongResponse.Content.ReadAsStringAsync());

        using var valid = fixture.CreateClient();
        var response = await fixture.Login(valid, fixture.OwnerUsername, fixture.OwnerPassword, "https://evil.example/");
        response.StatusCode.Should().Be(HttpStatusCode.Redirect);
        response.Headers.Location!.OriginalString.Should().Be("/admin/dashboard");
        var cookie = response.Headers.GetValues("Set-Cookie").Single(value => value.StartsWith("CompanyHQ.Auth="));
        cookie.ToLowerInvariant().Should().Contain("httponly").And.Contain("secure").And.Contain("samesite=lax");
    }

    [Fact]
    public async Task AdminIsDenyByDefaultAndResponsesHaveSecurityHeaders()
    {
        using var client = fixture.CreateClient();
        var response = await client.GetAsync("/admin/attribute-less-test");
        response.StatusCode.Should().Be(HttpStatusCode.Redirect);
        response.Headers.Location!.AbsolutePath.Should().Be("/auth/login");
        response.Headers.GetValues("X-Content-Type-Options").Should().Contain("nosniff");
        response.Headers.GetValues("X-Frame-Options").Should().Contain("DENY");
        response.Headers.Contains("Strict-Transport-Security").Should().BeTrue();
    }

    [Fact]
    public async Task LoginAndStateChangingFormsRejectMissingAntiforgeryToken()
    {
        using var client = fixture.CreateClient();
        var response = await client.PostAsync("/auth/login", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["Username"] = fixture.OwnerUsername, ["Password"] = fixture.OwnerPassword
        }));
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        using var authenticated = fixture.CreateClient();
        await fixture.Login(authenticated, fixture.OwnerUsername, fixture.OwnerPassword);
        var stateChange = await authenticated.PostAsync("/api/account/password", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["CurrentPassword"] = fixture.OwnerPassword, ["NewPassword"] = "AnotherValidPassword!789"
        }));
        stateChange.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task UsersApiIsAllowlistedAndNeverLeaksIdentitySecrets()
    {
        using var client = fixture.CreateClient();
        (await fixture.Login(client, fixture.OwnerUsername, fixture.OwnerPassword)).StatusCode.Should().Be(HttpStatusCode.Redirect);
        var json = await client.GetStringAsync("/api/admin/users");
        json.ToLowerInvariant().Should().NotContain("passwordhash")
            .And.NotContain("securitystamp")
            .And.NotContain("authenticator");
        using var document = JsonDocument.Parse(json);
        document.RootElement[0].EnumerateObject().Select(property => property.Name).Should().BeEquivalentTo("id", "username", "displayName", "email", "roles", "extensions");
    }

    [Fact]
    public async Task ShortAndCommonPasswordsAreRejected()
    {
        await using var scope = fixture.Factory.Services.CreateAsyncScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<CompanyUser>>();
        (await users.CreateAsync(new CompanyUser { UserName = "short-user" }, "Short1!")).Succeeded.Should().BeFalse();
        (await users.CreateAsync(new CompanyUser { UserName = "common-user" }, "password123")).Succeeded.Should().BeFalse();
    }

    [Fact]
    public async Task FiveFailuresLockAccountUntilLockoutExpires()
    {
        const string username = "lockout@company.test";
        const string password = "ValidPassword!234";
        await CreateUser(username, password, AuthRoles.Member);
        for (var attempt = 0; attempt < 5; attempt++)
        {
            using var client = fixture.CreateClient();
            (await fixture.Login(client, username, "WrongPassword!234")).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        }
        using (var locked = fixture.CreateClient())
            (await fixture.Login(locked, username, password)).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        await using (var scope = fixture.Factory.Services.CreateAsyncScope())
        {
            var users = scope.ServiceProvider.GetRequiredService<UserManager<CompanyUser>>();
            var user = (await users.FindByNameAsync(username))!;
            (await users.IsLockedOutAsync(user)).Should().BeTrue();
            await users.SetLockoutEndDateAsync(user, DateTimeOffset.UtcNow.AddSeconds(-1));
        }
        using var after = fixture.CreateClient();
        (await fixture.Login(after, username, password)).StatusCode.Should().Be(HttpStatusCode.Redirect);
    }

    [Fact]
    public async Task SixthRapidInvalidLoginIsRateLimitedWhileValidLockedCredentialStaysGeneric()
    {
        var username = $"rate-{Guid.NewGuid():N}@company.test";
        const string password = "ValidPassword!234";
        await CreateUser(username, password, AuthRoles.Member);
        for (var attempt = 0; attempt < 5; attempt++)
        {
            using var client = fixture.CreateClient();
            (await fixture.Login(client, username, "WrongPassword!234")).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        }
        using (var limited = fixture.CreateClient())
            (await fixture.Login(limited, username, "WrongPassword!234")).StatusCode.Should().Be(HttpStatusCode.TooManyRequests);
        using (var validButLocked = fixture.CreateClient())
            (await fixture.Login(validButLocked, username, password)).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task MemberCannotEscalateOrToggleServicesWhileOwnerCan()
    {
        const string username = "member@company.test";
        const string password = "ValidPassword!234";
        var member = await CreateUser(username, password, AuthRoles.Member);
        using var memberClient = fixture.CreateClient();
        await fixture.Login(memberClient, username, password);
        var memberToken = await fixture.GetAntiforgeryToken(memberClient);
        using var escalation = new HttpRequestMessage(HttpMethod.Put, $"/api/admin/users/{member.Id}/role?role=Owner");
        escalation.Headers.Add("RequestVerificationToken", memberToken);
        (await memberClient.SendAsync(escalation)).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        await using (var scope = fixture.Factory.Services.CreateAsyncScope())
            (await scope.ServiceProvider.GetRequiredService<UserManager<CompanyUser>>().IsInRoleAsync((await scope.ServiceProvider.GetRequiredService<UserManager<CompanyUser>>().FindByIdAsync(member.Id.ToString()))!, AuthRoles.Owner)).Should().BeFalse();

        using var owner = fixture.CreateClient();
        await fixture.Login(owner, fixture.OwnerUsername, fixture.OwnerPassword);
        var ownerToken = await fixture.GetAntiforgeryToken(owner);
        using var toggle = new HttpRequestMessage(HttpMethod.Put, "/api/admin/services/orders/enabled?enabled=false");
        toggle.Headers.Add("RequestVerificationToken", ownerToken);
        (await owner.SendAsync(toggle)).StatusCode.Should().Be(HttpStatusCode.NoContent);
        var authToken = await fixture.GetAntiforgeryToken(owner);
        using var disableAuth = new HttpRequestMessage(HttpMethod.Put, "/api/admin/services/auth/enabled?enabled=false");
        disableAuth.Headers.Add("RequestVerificationToken", authToken);
        (await owner.SendAsync(disableAuth)).StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var restoreToken = await fixture.GetAntiforgeryToken(owner);
        using var restore = new HttpRequestMessage(HttpMethod.Put, "/api/admin/services/orders/enabled?enabled=true");
        restore.Headers.Add("RequestVerificationToken", restoreToken);
        (await owner.SendAsync(restore)).StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task OrdersArePersistedAndViewerCannotCreateWhileMemberCan()
    {
        var viewerName = $"viewer-{Guid.NewGuid():N}@company.test";
        var memberName = $"member-{Guid.NewGuid():N}@company.test";
        const string password = "ValidPassword!234";
        await CreateUser(viewerName, password, AuthRoles.Viewer);
        await CreateUser(memberName, password, AuthRoles.Member);

        using var viewer = fixture.CreateClient();
        await fixture.Login(viewer, viewerName, password);
        var viewerToken = await fixture.GetAntiforgeryToken(viewer);
        using var denied = new HttpRequestMessage(HttpMethod.Post, "/api/services/orders") { Content = JsonContent.Create(new { slug = "denied", total = 1m }) };
        denied.Headers.Add("RequestVerificationToken", viewerToken);
        (await viewer.SendAsync(denied)).StatusCode.Should().Be(HttpStatusCode.Forbidden);

        using var member = fixture.CreateClient();
        await fixture.Login(member, memberName, password);
        var memberToken = await fixture.GetAntiforgeryToken(member);
        var slug = "order-" + Guid.NewGuid().ToString("N");
        using var create = new HttpRequestMessage(HttpMethod.Post, "/api/services/orders") { Content = JsonContent.Create(new { slug, total = 42.50m }) };
        create.Headers.Add("RequestVerificationToken", memberToken);
        (await member.SendAsync(create)).StatusCode.Should().Be(HttpStatusCode.Created);
        (await member.GetStringAsync("/api/services/orders")).Should().Contain(slug).And.Contain("42.50");
    }

    [Theory]
    [InlineData(AuthRoles.Viewer, HttpStatusCode.Forbidden)]
    [InlineData(AuthRoles.Member, HttpStatusCode.Forbidden)]
    [InlineData(AuthRoles.Admin, HttpStatusCode.NotFound)]
    [InlineData(AuthRoles.Owner, HttpStatusCode.NotFound)]
    public async Task ManagementPolicyMatrixIsEnforcedByDirectRequests(string role, HttpStatusCode expected)
    {
        var username = $"matrix-{role}-{Guid.NewGuid():N}@company.test";
        const string password = "ValidPassword!234";
        await CreateUser(username, password, role);
        using var client = fixture.CreateClient();
        await fixture.Login(client, username, password);
        var token = await fixture.GetAntiforgeryToken(client);
        using var request = new HttpRequestMessage(HttpMethod.Delete, $"/api/admin/users/{Guid.NewGuid()}");
        request.Headers.Add("RequestVerificationToken", token);
        (await client.SendAsync(request)).StatusCode.Should().Be(expected);
    }

    [Fact]
    public async Task AnonymousIsDeniedFromEveryProtectedSurfaceInTheMatrix()
    {
        using var client = fixture.CreateClient();
        foreach (var route in new[] { "/api/admin/users", "/api/scaffolding/services", "/api/services/orders" })
            (await client.GetAsync(route)).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await client.GetAsync("/admin")).Headers.Location!.AbsolutePath.Should().Be("/auth/login");
    }

    [Theory]
    [InlineData(AuthRoles.Viewer, HttpStatusCode.Forbidden)]
    [InlineData(AuthRoles.Member, HttpStatusCode.Forbidden)]
    [InlineData(AuthRoles.Admin, HttpStatusCode.Forbidden)]
    [InlineData(AuthRoles.Owner, HttpStatusCode.OK)]
    public async Task OwnerOnlyPolicyMatrixIsEnforcedByDirectRequests(string role, HttpStatusCode expected)
    {
        var username = $"owner-matrix-{role}-{Guid.NewGuid():N}@company.test";
        const string password = "ValidPassword!234";
        await CreateUser(username, password, role);
        using var client = fixture.CreateClient();
        await fixture.Login(client, username, password);
        (await client.GetAsync("/api/admin/model/entities")).StatusCode.Should().Be(expected);
    }

    [Fact]
    public void EveryApplicationEndpointIsExplicitlyInventoriedInTheAuthorizationMatrix()
    {
        var actual = fixture.Factory.Services.GetRequiredService<EndpointDataSource>().Endpoints.OfType<RouteEndpoint>()
            .Where(endpoint => endpoint.RoutePattern.RawText is { } route && (route.StartsWith("/api") || route.StartsWith("/auth") || route.StartsWith("/internal") || route is "/health" or "/companies"))
            .SelectMany(endpoint => (endpoint.Metadata.GetMetadata<IHttpMethodMetadata>()?.HttpMethods ?? ["ANY"]).Select(method => $"{method} {endpoint.RoutePattern.RawText}"))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var expected = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "ANY /health", "GET /companies", "GET /auth/login", "POST /auth/login", "POST /auth/logout", "POST /api/account/password", "GET /auth/denied",
            "POST /internal/bootstrap/owner", "GET /api/admin/users", "PUT /api/admin/users/{id:guid}/role", "DELETE /api/admin/users/{id:guid}",
            "POST /api/companies", "POST /api/companies/{id:guid}/delete", "GET /api/admin/model/entities", "POST /api/admin/model/fields", "POST /api/admin/model/links",
            "PUT /api/admin/model/users/{id:guid}/extensions/{fieldName}", "GET /api/admin/model/users/{id:guid}/related",
            "GET /api/scaffolding/services", "POST /api/scaffolding/services", "GET /api/services/sales", "GET /api/services/orders", "POST /api/services/orders",
            "POST /api/services/orders/form", "GET /api/services/products", "GET /api/services/quotes", "GET /api/services/{serviceId}", "PUT /api/admin/services/{id}/enabled"
        };
        actual.Should().BeEquivalentTo(expected, "every new application endpoint must be assigned to the documented role matrix");
    }

    [Theory]
    [InlineData(AuthRoles.Viewer)]
    [InlineData(AuthRoles.Member)]
    [InlineData(AuthRoles.Admin)]
    [InlineData(AuthRoles.Owner)]
    public async Task AuthenticatedFallbackPolicyAllowsEveryRoleToReadServiceData(string role)
    {
        var username = $"read-matrix-{role}-{Guid.NewGuid():N}@company.test";
        const string password = "ValidPassword!234";
        await CreateUser(username, password, role);
        using var client = fixture.CreateClient();
        await fixture.Login(client, username, password);
        (await client.GetAsync("/api/services/orders")).StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task LastOwnerCannotBeDemotedOrDeleted()
    {
        await using var scope = fixture.Factory.Services.CreateAsyncScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<CompanyUser>>();
        var ownerUser = (await users.FindByNameAsync(fixture.OwnerUsername))!;
        foreach (var extraOwner in (await users.GetUsersInRoleAsync(AuthRoles.Owner)).Where(candidate => candidate.Id != ownerUser.Id))
        {
            await users.RemoveFromRoleAsync(extraOwner, AuthRoles.Owner);
            await users.AddToRoleAsync(extraOwner, AuthRoles.Member);
        }
        using var owner = fixture.CreateClient();
        await fixture.Login(owner, fixture.OwnerUsername, fixture.OwnerPassword);
        var token = await fixture.GetAntiforgeryToken(owner);
        using var demote = new HttpRequestMessage(HttpMethod.Put, $"/api/admin/users/{ownerUser.Id}/role?role=Member");
        demote.Headers.Add("RequestVerificationToken", token);
        (await owner.SendAsync(demote)).StatusCode.Should().Be(HttpStatusCode.Conflict);
        using var delete = new HttpRequestMessage(HttpMethod.Delete, $"/api/admin/users/{ownerUser.Id}");
        delete.Headers.Add("RequestVerificationToken", token);
        (await owner.SendAsync(delete)).StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task OnlyOwnerCanDeleteACompanyAfterTypingItsExactName()
    {
        var company = new Services.Companies.Models.RegisteredCompany
        {
            Name = "Acme Industries", Slug = "acme-industries", Host = "acme.test", AdminUrl = "https://acme.test/admin", DbProvider = "postgres", DatabaseName = "companyhq", DeploymentStatus = "running"
        };
        var deployment = Path.Combine(fixture.RootFolder, "companies", company.Slug);
        Directory.CreateDirectory(deployment);
        File.WriteAllText(Path.Combine(deployment, "company.json"), "{}");
        await using (var scope = fixture.Factory.Services.CreateAsyncScope())
            await scope.ServiceProvider.GetRequiredService<Services.Companies.Data.ICompanyRegistry>().Register(company);

        const string password = "ValidPassword!234";
        var member = await CreateUser("delete-member@company.test", password, AuthRoles.Member);
        using (var memberClient = fixture.CreateClient())
        {
            await fixture.Login(memberClient, member.UserName!, password);
            var token = await fixture.GetAntiforgeryToken(memberClient);
            using var forbidden = new HttpRequestMessage(HttpMethod.Post, $"/api/companies/{company.Id}/delete") { Content = new FormUrlEncodedContent(new Dictionary<string, string> { ["confirmation"] = company.Name }) };
            forbidden.Headers.Add("RequestVerificationToken", token);
            (await memberClient.SendAsync(forbidden)).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        }

        using var owner = fixture.CreateClient();
        await fixture.Login(owner, fixture.OwnerUsername, fixture.OwnerPassword);
        var wrongToken = await fixture.GetAntiforgeryToken(owner);
        using var wrongConfirmation = new HttpRequestMessage(HttpMethod.Post, $"/api/companies/{company.Id}/delete") { Content = new FormUrlEncodedContent(new Dictionary<string, string> { ["confirmation"] = "Acme" }) };
        wrongConfirmation.Headers.Add("RequestVerificationToken", wrongToken);
        (await owner.SendAsync(wrongConfirmation)).StatusCode.Should().Be(HttpStatusCode.BadRequest);
        Directory.Exists(deployment).Should().BeTrue();

        var deleteToken = await fixture.GetAntiforgeryToken(owner);
        using var delete = new HttpRequestMessage(HttpMethod.Post, $"/api/companies/{company.Id}/delete") { Content = new FormUrlEncodedContent(new Dictionary<string, string> { ["confirmation"] = company.Name }) };
        delete.Headers.Add("RequestVerificationToken", deleteToken);
        (await owner.SendAsync(delete)).StatusCode.Should().Be(HttpStatusCode.Redirect);
        Directory.Exists(deployment).Should().BeFalse();
        await using var verificationScope = fixture.Factory.Services.CreateAsyncScope();
        (await verificationScope.ServiceProvider.GetRequiredService<Services.Companies.Data.ICompanyRegistry>().GetCompany(company.Id)).Should().BeNull();
    }

    [Fact]
    public async Task PasswordChangeInvalidatesOtherSessions()
    {
        const string username = "sessions@company.test";
        const string password = "ValidPassword!234";
        await CreateUser(username, password, AuthRoles.Member);
        using var first = fixture.CreateClient();
        using var second = fixture.CreateClient();
        await fixture.Login(first, username, password);
        await fixture.Login(second, username, password);
        var token = await fixture.GetAntiforgeryToken(first);
        var response = await first.PostAsync("/api/account/password", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["CurrentPassword"] = password,
            ["NewPassword"] = "NewValidPassword!567",
            ["__RequestVerificationToken"] = token
        }));
        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        var otherSession = await second.GetAsync("/admin/dashboard");
        otherSession.StatusCode.Should().Be(HttpStatusCode.Redirect);
        otherSession.Headers.Location!.AbsolutePath.Should().Be("/auth/login");
    }

    [Fact]
    public async Task LogoutInvalidatesReplayedCookieServerSide()
    {
        const string username = "logout@company.test";
        const string password = "ValidPassword!234";
        await CreateUser(username, password, AuthRoles.Member);
        using var client = fixture.CreateClient();
        var login = await fixture.Login(client, username, password);
        var oldCookie = login.Headers.GetValues("Set-Cookie").Single(value => value.StartsWith("CompanyHQ.Auth=")).Split(';')[0];
        var token = await fixture.GetAntiforgeryToken(client);
        var logout = await client.PostAsync("/auth/logout", new FormUrlEncodedContent(new Dictionary<string, string> { ["__RequestVerificationToken"] = token }));
        logout.StatusCode.Should().Be(HttpStatusCode.Redirect);

        using var replay = fixture.CreateClient(cookies: false);
        replay.DefaultRequestHeaders.Add("Cookie", oldCookie);
        var response = await replay.GetAsync("/admin/dashboard");
        response.StatusCode.Should().Be(HttpStatusCode.Redirect);
        response.Headers.Location!.AbsolutePath.Should().Be("/auth/login");
    }

    [Fact]
    public async Task CookieFromAnotherCompanyKeyRingIsRejected()
    {
        using var companyA = fixture.CreateClient();
        var login = await fixture.Login(companyA, fixture.OwnerUsername, fixture.OwnerPassword);
        var cookie = login.Headers.GetValues("Set-Cookie").Single(value => value.StartsWith("CompanyHQ.Auth=")).Split(';')[0];
        var companyBKeys = Path.Combine(fixture.RootFolder, "company-b-keys");
        Directory.CreateDirectory(companyBKeys);
        using var companyBFactory = new Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Production");
            builder.UseSetting("ModulesPath", Path.Combine(fixture.RootFolder, "modules"));
            builder.UseSetting("Database:Provider", "postgres");
            builder.UseSetting("ConnectionStrings:Company", fixture.ConnectionString);
            builder.UseSetting("DataProtection:Path", companyBKeys);
            builder.UseSetting("Company:Id", Guid.NewGuid().ToString());
        });
        using var companyB = companyBFactory.CreateClient(new Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost"), AllowAutoRedirect = false, HandleCookies = false });
        companyB.DefaultRequestHeaders.Add("Cookie", cookie);
        var response = await companyB.GetAsync("/admin/dashboard");
        response.StatusCode.Should().Be(HttpStatusCode.Redirect);
        response.Headers.Location!.AbsolutePath.Should().Be("/auth/login");
    }

    [Fact]
    public async Task ExpiredCookieRedirectsToLoginInsteadOfFailing()
    {
        var keys = Path.Combine(fixture.RootFolder, "short-cookie-keys");
        Directory.CreateDirectory(keys);
        using var shortFactory = new Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Production");
            builder.UseSetting("ModulesPath", Path.Combine(fixture.RootFolder, "modules"));
            builder.UseSetting("Database:Provider", "postgres");
            builder.UseSetting("ConnectionStrings:Company", fixture.ConnectionString);
            builder.UseSetting("DataProtection:Path", keys);
            builder.UseSetting("Company:Id", "short-cookie-company");
            builder.ConfigureServices(services => services.PostConfigure<CookieAuthenticationOptions>(IdentityConstants.ApplicationScheme, options =>
            {
                options.ExpireTimeSpan = TimeSpan.FromMilliseconds(100);
                options.SlidingExpiration = false;
            }));
        });
        using var client = shortFactory.CreateClient(new Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost"), AllowAutoRedirect = false, HandleCookies = true });
        var html = await client.GetStringAsync("/auth/login");
        var token = Regex.Match(html, "name=\"__RequestVerificationToken\" value=\"([^\"]+)\"").Groups[1].Value;
        (await client.PostAsync("/auth/login", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["Username"] = fixture.OwnerUsername, ["Password"] = fixture.OwnerPassword, ["__RequestVerificationToken"] = token
        }))).StatusCode.Should().Be(HttpStatusCode.Redirect);
        await Task.Delay(250);
        var expired = await client.GetAsync("/admin/dashboard");
        expired.StatusCode.Should().Be(HttpStatusCode.Redirect);
        expired.Headers.Location!.AbsolutePath.Should().Be("/auth/login");
    }

    [Fact]
    public async Task ProductionErrorAndLoginPagesLeakNoInternals()
    {
        using var client = fixture.CreateClient();
        var error = (await client.GetStringAsync("/Error")).ToLowerInvariant();
        error.Should().NotContain("npgsql").And.NotContain("password=").And.NotContain("stack trace");
        var login = (await (await fixture.Login(client, "nobody@invalid.test", "WrongPassword!123")).Content.ReadAsStringAsync()).ToLowerInvariant();
        login.Should().NotContain("npgsql").And.NotContain("connection").And.NotContain("stack trace");
        var logs = string.Join('\n', fixture.Logs.Messages);
        logs.Should().NotContain(fixture.OwnerPassword).And.NotContain(new NpgsqlConnectionStringBuilder(fixture.ConnectionString).Password);
    }

    private async Task<CompanyUser> CreateUser(string username, string password, string role)
    {
        await using var scope = fixture.Factory.Services.CreateAsyncScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<CompanyUser>>();
        var existing = await users.FindByNameAsync(username);
        if (existing is not null) return existing;
        var user = new CompanyUser { UserName = username, Email = username, DisplayName = username };
        (await users.CreateAsync(user, password)).Succeeded.Should().BeTrue();
        await users.AddToRoleAsync(user, role);
        return user;
    }
}
