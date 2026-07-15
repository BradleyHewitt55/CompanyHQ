using System.Net;
using System.Net.Http.Json;
using Services.Auth;
using Services.Auth.Models;
using Services.Companies.Contracts.Services;
using FluentAssertions;
using FluentAssertions.Execution;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Http.Metadata;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace Web.Tests;

/// <summary>
/// P9 authorization matrix: every application endpoint is assigned an expected server-side
/// allow/deny outcome for Anonymous, Viewer, Member, Admin, and Owner, and every outcome is
/// exercised by a direct HTTP request that bypasses the UI. An endpoint that is added without
/// a matrix row fails <see cref="TheMatrixCoversEveryApplicationEndpoint"/>.
/// </summary>
[Collection(AuthCollection.Name)]
public sealed class AuthorizationMatrixTests(AuthHostFixture fixture)
{
    public const string Anonymous = "Anonymous";

    /// <summary>Expected server-side outcome for one role on one endpoint.</summary>
    private enum Expect
    {
        Ok,                  // 200 — role is allowed and the endpoint serves
        Allowed,             // authorization passed: anything except 401/403/redirect-to-login
        Unauthorized,        // 401 — anonymous rejected at the authentication layer
        Forbidden,           // 403 — authenticated but role denied server-side
        LoginRedirect,       // page routes: anonymous is redirected to /auth/login
        BadRequest,          // authorization passed, request rejected before any effect (antiforgery/body)
        NotFound,            // authorization passed, probe resource does not exist
        MovedPermanently,    // 301 — public permanent redirect
        ForbiddenByDesign,   // /auth/denied returns 403 for everyone on purpose
        TokenGated           // 401/410 — gated by a machine token, never by the caller's role
    }

    private sealed record Rule(Func<HttpRequestMessage> Request, Expect Anonymous, Expect Viewer, Expect Member, Expect Admin, Expect Owner);

    private static HttpRequestMessage Get(string url) => new(HttpMethod.Get, url);
    private static HttpRequestMessage Send(HttpMethod method, string url, object? json = null) =>
        json is null ? new HttpRequestMessage(method, url) : new HttpRequestMessage(method, url) { Content = JsonContent.Create(json) };

    /// <summary>
    /// The authorization matrix. Keys must match the endpoint inventory ("METHOD ROUTE-TEMPLATE").
    /// Probe requests use random ids and omit antiforgery tokens so no request has a side effect:
    /// for mutating endpoints an allowed role proves authorization passed by reaching the
    /// antiforgery/body-binding rejection (400) or a not-found probe (404) instead of 401/403.
    /// </summary>
    private static IReadOnlyDictionary<string, Rule> Matrix { get; } = new Dictionary<string, Rule>(StringComparer.OrdinalIgnoreCase)
    {
        ["ANY /health"] = new(() => Get("/health"), Expect.Ok, Expect.Ok, Expect.Ok, Expect.Ok, Expect.Ok),
        ["GET /companies"] = new(() => Get("/companies"), Expect.MovedPermanently, Expect.MovedPermanently, Expect.MovedPermanently, Expect.MovedPermanently, Expect.MovedPermanently),
        ["GET /auth/login"] = new(() => Get("/auth/login"), Expect.Ok, Expect.Ok, Expect.Ok, Expect.Ok, Expect.Ok),
        ["POST /auth/login"] = new(() => Send(HttpMethod.Post, "/auth/login"), Expect.BadRequest, Expect.BadRequest, Expect.BadRequest, Expect.BadRequest, Expect.BadRequest),
        ["POST /auth/logout"] = new(() => Send(HttpMethod.Post, "/auth/logout"), Expect.LoginRedirect, Expect.BadRequest, Expect.BadRequest, Expect.BadRequest, Expect.BadRequest),
        ["GET /auth/denied"] = new(() => Get("/auth/denied"), Expect.ForbiddenByDesign, Expect.ForbiddenByDesign, Expect.ForbiddenByDesign, Expect.ForbiddenByDesign, Expect.ForbiddenByDesign),
        ["POST /api/account/password"] = new(() => Send(HttpMethod.Post, "/api/account/password"), Expect.Unauthorized, Expect.BadRequest, Expect.BadRequest, Expect.BadRequest, Expect.BadRequest),
        ["POST /internal/bootstrap/owner"] = new(() => Send(HttpMethod.Post, "/internal/bootstrap/owner?username=matrix-probe"), Expect.TokenGated, Expect.TokenGated, Expect.TokenGated, Expect.TokenGated, Expect.TokenGated),
        ["POST /api/companies"] = new(() => Send(HttpMethod.Post, "/api/companies", new { name = "probe", host = "probe.test", adminUrl = "https://probe.test/admin", dbProvider = "postgres" }), Expect.Unauthorized, Expect.Unauthorized, Expect.Unauthorized, Expect.Unauthorized, Expect.Unauthorized),
        ["POST /api/companies/{id:guid}/delete"] = new(() => Send(HttpMethod.Post, $"/api/companies/{Guid.NewGuid()}/delete"), Expect.Unauthorized, Expect.Forbidden, Expect.Forbidden, Expect.Forbidden, Expect.BadRequest),
        ["GET /api/admin/users"] = new(() => Get("/api/admin/users"), Expect.Unauthorized, Expect.Ok, Expect.Ok, Expect.Ok, Expect.Ok),
        ["PUT /api/admin/users/{id:guid}/role"] = new(() => Send(HttpMethod.Put, $"/api/admin/users/{Guid.NewGuid()}/role?role=Member"), Expect.Unauthorized, Expect.Forbidden, Expect.Forbidden, Expect.BadRequest, Expect.BadRequest),
        ["DELETE /api/admin/users/{id:guid}"] = new(() => Send(HttpMethod.Delete, $"/api/admin/users/{Guid.NewGuid()}"), Expect.Unauthorized, Expect.Forbidden, Expect.Forbidden, Expect.NotFound, Expect.NotFound),
        ["GET /api/admin/model/entities"] = new(() => Get("/api/admin/model/entities"), Expect.Unauthorized, Expect.Forbidden, Expect.Forbidden, Expect.Forbidden, Expect.Ok),
        ["POST /api/admin/model/fields"] = new(() => Send(HttpMethod.Post, "/api/admin/model/fields"), Expect.Unauthorized, Expect.Forbidden, Expect.Forbidden, Expect.Forbidden, Expect.BadRequest),
        ["POST /api/admin/model/links"] = new(() => Send(HttpMethod.Post, "/api/admin/model/links"), Expect.Unauthorized, Expect.Forbidden, Expect.Forbidden, Expect.Forbidden, Expect.BadRequest),
        ["PUT /api/admin/model/users/{id:guid}/extensions/{fieldName}"] = new(() => Send(HttpMethod.Put, $"/api/admin/model/users/{Guid.NewGuid()}/extensions/matrix-probe", "probe"), Expect.Unauthorized, Expect.Forbidden, Expect.Forbidden, Expect.Forbidden, Expect.NotFound),
        ["GET /api/admin/model/users/{id:guid}/related"] = new(() => Get($"/api/admin/model/users/{Guid.NewGuid()}/related"), Expect.Unauthorized, Expect.Allowed, Expect.Allowed, Expect.Allowed, Expect.Allowed),
        ["GET /api/scaffolding/services"] = new(() => Get("/api/scaffolding/services"), Expect.Unauthorized, Expect.Ok, Expect.Ok, Expect.Ok, Expect.Ok),
        ["POST /api/scaffolding/services"] = new(() => Send(HttpMethod.Post, "/api/scaffolding/services"), Expect.Unauthorized, Expect.Forbidden, Expect.Forbidden, Expect.BadRequest, Expect.BadRequest),
        ["GET /api/services/sales"] = new(() => Get("/api/services/sales"), Expect.Unauthorized, Expect.Ok, Expect.Ok, Expect.Ok, Expect.Ok),
        ["GET /api/services/orders"] = new(() => Get("/api/services/orders"), Expect.Unauthorized, Expect.Ok, Expect.Ok, Expect.Ok, Expect.Ok),
        ["GET /api/services/quotes"] = new(() => Get("/api/services/quotes"), Expect.Unauthorized, Expect.Ok, Expect.Ok, Expect.Ok, Expect.Ok),
        ["GET /api/services/products"] = new(() => Get("/api/services/products"), Expect.Unauthorized, Expect.Ok, Expect.Ok, Expect.Ok, Expect.Ok),
        ["POST /api/services/orders"] = new(() => Send(HttpMethod.Post, "/api/services/orders"), Expect.Unauthorized, Expect.Forbidden, Expect.BadRequest, Expect.BadRequest, Expect.BadRequest),
        ["POST /api/services/orders/form"] = new(() => Send(HttpMethod.Post, "/api/services/orders/form"), Expect.Unauthorized, Expect.Forbidden, Expect.BadRequest, Expect.BadRequest, Expect.BadRequest),
        ["GET /api/services/{serviceId}"] = new(() => Get("/api/services/matrix-unknown-service"), Expect.Unauthorized, Expect.NotFound, Expect.NotFound, Expect.NotFound, Expect.NotFound),
        ["PUT /api/admin/services/{id}/enabled"] = new(() => Send(HttpMethod.Put, "/api/admin/services/matrix-unknown-service/enabled?enabled=true"), Expect.Unauthorized, Expect.Forbidden, Expect.Forbidden, Expect.BadRequest, Expect.BadRequest),
    };

    /// <summary>Admin page routes rendered by the Blazor pipeline: anonymous is redirected to login, every authenticated role is served.</summary>
    private static IReadOnlyDictionary<string, Rule> Pages { get; } = new Dictionary<string, Rule>(StringComparer.OrdinalIgnoreCase)
    {
        ["GET /"] = new(() => Get("/"), Expect.Ok, Expect.Ok, Expect.Ok, Expect.Ok, Expect.Ok),
        ["GET /admin"] = new(() => Get("/admin"), Expect.LoginRedirect, Expect.Ok, Expect.Ok, Expect.Ok, Expect.Ok),
        ["GET /admin/dashboard"] = new(() => Get("/admin/dashboard"), Expect.LoginRedirect, Expect.Ok, Expect.Ok, Expect.Ok, Expect.Ok),
        ["GET /admin/companies"] = new(() => Get("/admin/companies"), Expect.LoginRedirect, Expect.Ok, Expect.Ok, Expect.Ok, Expect.Ok),
        ["GET /admin/users"] = new(() => Get("/admin/users"), Expect.LoginRedirect, Expect.Ok, Expect.Ok, Expect.Ok, Expect.Ok),
        ["GET /admin/services"] = new(() => Get("/admin/services"), Expect.LoginRedirect, Expect.Ok, Expect.Ok, Expect.Ok, Expect.Ok),
    };

    [Fact]
    public void TheMatrixCoversEveryApplicationEndpoint()
    {
        var discovered = fixture.Factory.Services.GetRequiredService<EndpointDataSource>().Endpoints.OfType<RouteEndpoint>()
            .Where(endpoint => endpoint.RoutePattern.RawText is { } route && (route.StartsWith("/api") || route.StartsWith("/auth") || route.StartsWith("/internal") || route is "/health" or "/companies"))
            .SelectMany(endpoint => (endpoint.Metadata.GetMetadata<IHttpMethodMetadata>()?.HttpMethods ?? ["ANY"]).Select(method => $"{method} {endpoint.RoutePattern.RawText}"))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        discovered.Should().BeEquivalentTo(Matrix.Keys, "every application endpoint must have an authorization matrix row for all five roles; add new endpoints to AuthorizationMatrixTests.Matrix");
    }

    [Theory]
    [InlineData(Anonymous)]
    [InlineData(AuthRoles.Viewer)]
    [InlineData(AuthRoles.Member)]
    [InlineData(AuthRoles.Admin)]
    [InlineData(AuthRoles.Owner)]
    public async Task EveryEndpointEnforcesTheMatrixForRole(string role)
    {
        await EnsureSampleServicesEnabled();
        using var client = await ClientFor(role);
        using var scope = new AssertionScope();
        foreach (var (endpoint, rule) in Matrix.Concat(Pages))
        {
            using var request = rule.Request();
            using var response = await client.SendAsync(request);
            AssertOutcome(endpoint, role, response, ExpectationFor(rule, role));
        }
    }

    private static Expect ExpectationFor(Rule rule, string role) => role switch
    {
        Anonymous => rule.Anonymous,
        AuthRoles.Viewer => rule.Viewer,
        AuthRoles.Member => rule.Member,
        AuthRoles.Admin => rule.Admin,
        AuthRoles.Owner => rule.Owner,
        _ => throw new ArgumentOutOfRangeException(nameof(role), role, null)
    };

    private static void AssertOutcome(string endpoint, string role, HttpResponseMessage response, Expect expected)
    {
        var because = $"{role} on '{endpoint}' must produce {expected}";
        switch (expected)
        {
            case Expect.Ok: response.StatusCode.Should().Be(HttpStatusCode.OK, because); break;
            case Expect.Unauthorized: response.StatusCode.Should().Be(HttpStatusCode.Unauthorized, because); break;
            case Expect.Forbidden or Expect.ForbiddenByDesign: response.StatusCode.Should().Be(HttpStatusCode.Forbidden, because); break;
            case Expect.BadRequest: response.StatusCode.Should().Be(HttpStatusCode.BadRequest, because); break;
            case Expect.NotFound: response.StatusCode.Should().Be(HttpStatusCode.NotFound, because); break;
            case Expect.MovedPermanently: response.StatusCode.Should().Be(HttpStatusCode.MovedPermanently, because); break;
            case Expect.TokenGated: response.StatusCode.Should().BeOneOf([HttpStatusCode.Unauthorized, HttpStatusCode.Gone], because); break;
            case Expect.LoginRedirect:
                ((int)response.StatusCode).Should().BeInRange(300, 399, because);
                LocationPath(response).Should().StartWith("/auth/login", because);
                break;
            case Expect.Allowed:
                response.StatusCode.Should().NotBe(HttpStatusCode.Unauthorized, because).And.NotBe(HttpStatusCode.Forbidden, because);
                if ((int)response.StatusCode is >= 300 and < 400)
                    LocationPath(response).Should().NotStartWith("/auth/login", because);
                break;
        }
    }

    private static string LocationPath(HttpResponseMessage response) =>
        response.Headers.Location is { } location ? (location.IsAbsoluteUri ? location.PathAndQuery : location.OriginalString) : string.Empty;

    private async Task<HttpClient> ClientFor(string role)
    {
        var client = fixture.CreateClient();
        if (role == Anonymous) return client;
        var username = $"authz-matrix-{role}@company.test".ToLowerInvariant();
        const string password = "ValidPassword!234";
        await using (var scope = fixture.Factory.Services.CreateAsyncScope())
        {
            var users = scope.ServiceProvider.GetRequiredService<UserManager<CompanyUser>>();
            if (await users.FindByNameAsync(username) is null)
            {
                var user = new CompanyUser { UserName = username, Email = username, DisplayName = username };
                (await users.CreateAsync(user, password)).Succeeded.Should().BeTrue();
                (await users.AddToRoleAsync(user, role)).Succeeded.Should().BeTrue();
            }
        }
        using var login = await fixture.Login(client, username, password);
        ((int)login.StatusCode).Should().BeInRange(300, 399, $"the {role} matrix user must be able to sign in (login redirects on success)");
        return client;
    }

    private async Task EnsureSampleServicesEnabled()
    {
        var states = fixture.Factory.Services.GetRequiredService<IServiceStateStore>();
        foreach (var id in new[] { "sales", "orders", "quotes", "products" })
            if (!states.IsEnabled(id)) await states.SetEnabled(id, true);
    }
}
