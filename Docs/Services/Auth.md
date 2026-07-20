# Auth

This guide is for hosts that need company-bound login, registration, and authorization with service-owned Admin pages.

## Directory responsibilities

| Directory | Responsibility |
|---|---|
| `Api/` | HTTP controllers exposed by the service. Controllers follow `{Service}Controller`; [`AuthController`][8] owns Auth routes, cookies, redirects, and DTO responses. |
| `Components/` | Reusable Auth-specific UI such as [`LoginForm`][11] and [`RegisterForm`][12]. Components submit to the Auth API and contain no authentication logic. |
| `Dtos/` | Outbound API contracts. [`AdminUserDto`][9] and [`ClientUserDto`][10] expose safe user data and never expose `PasswordHash`. |
| `Layout/` | Service-owned layouts for Auth routes. [`AuthLayout`][7] inherits `LayoutComponentBase` and provides the plain, centered authentication surface. |
| `Models/` | Inbound form and API request models such as [`LoginModel`][13] and [`RegisterModel`][14]. Request models are not database entities or response DTOs. |
| `Pages/` | Routable service pages loaded into the Admin host. [`Login.razor`][5] and [`Register.razor`][6] select `AuthLayout` and compose Auth components. |
| `Services/` | Headless application logic registered through dependency injection. [`AuthService`][2] handles credentials and registration; [`UserService`][15] handles user records. |
| `Services/Authorization/` | Authorization services: [`CurrentCompanyCookieEvents`][16] validates tenant-bound cookies, [`CurrentCompanyRequirement`][17] enforces company access, and [`AuthClaims`][18] defines policy and claim names. |

Root files define service integration: [`AuthPlugin`][1] registers dependencies, controllers, authentication, and authorization; [`Auth.csproj`][19] defines the service package.

DTOs stay in `Auth/Dtos` because they are Auth API contracts. Provider-neutral entities and interfaces stay in `Projects/Abstractions`; request models stay in `Auth/Models`.

## I only need the default setup

[`AuthPlugin`][1] is loaded through the shared registry beside Companies and a
data-provider plugin — hosts never construct it directly (see [Plugins](../Architecture/Plugins.md)):

```csharp
builder.Services.AddCompanyHqPlugins(builder.Configuration);
```

Place company resolution before the plugin middleware, then map the Auth controller:

```csharp
app.UseMiddleware<CompanyResolutionMiddleware>();
app.UseCompanyHqPlugins();   // Auth adds RateLimiter, Authentication, Authorization
app.UseAntiforgery();
app.MapControllers();
```

[`AuthPlugin.ConfigureApplication`][1] owns the rate limiter, authentication, and
authorization middleware. The host must not add them itself.

[`AuthPlugin.ConfigureServices`][1] registers [`IAuthService`][2], [`IUserService`][3], scoped [`ICurrentUser`][4], password hashing, MVC controllers, cookie authentication, the credential rate-limit policy, and company authorization policies.

## Security defaults

- **Cookie policy.** [`AuthPlugin`][1] issues the `CompanyHq.Auth` cookie as `HttpOnly`, `Secure` (always — TLS is assumed at the edge), `SameSite=Lax`, with a 12-hour sliding expiration.
- **Session revalidation.** [`CurrentCompanyCookieEvents`][16] runs on every authenticated request: it rejects cookies whose `company` claim does not match the resolved company, reloads the user and rejects cookies for users that no longer exist, populates [`ICurrentUser`][4], and deletes the cookie on rejection. Hosts do not need their own current-user middleware.
- **Credential rate limiting.** `POST /api/auth/login` and `POST /api/auth/register` (form and JSON variants alike) are throttled to 5 attempts per client IP per minute (`429` beyond that) under the `AuthClaims.CredentialRateLimitPolicy` policy. The host must call `app.UseRateLimiter()` and, behind a reverse proxy, forward `X-Forwarded-For` so the partition key is the client, not the proxy.
- **Password policy.** [`PasswordPolicy`][20] (8-128 characters, length-only per NIST 800-63B) is enforced by [`RegisterModel`][14] validation, by [`AuthService`][2]/[`UserService`][15] at the service boundary, and by the CLI's company-creation prompt.
- **Login hygiene.** [`AuthService`][2] verifies against a dummy hash when the account does not exist (no user enumeration by timing) and transparently re-hashes passwords when the hasher's cost parameters change.
- **API status codes.** Unauthenticated `/api` requests get `401` and forbidden ones `403` instead of login-page redirects.

## I want the Auth pages in my Admin host

Feed `CompanyHqPlugins.RouteAssemblies` to both Razor endpoint discovery and the
router — the plugin declares its own assembly through [`RouteAssemblies`][22], so
the host never names Auth types. [`Login.razor`][5] and [`Register.razor`][6] select the service-owned [`AuthLayout`][7], which inherits `LayoutComponentBase` and renders a centered form on a plain background. Other Admin pages continue to use the host's main layout.

```csharp
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode()
    .AddAdditionalAssemblies(CompanyHqPlugins.RouteAssemblies);
```

```razor
<Router AppAssembly="typeof(Program).Assembly"
        AdditionalAssemblies="CompanyHqPlugins.RouteAssemblies">
    <Found Context="routeData">
        <RouteView RouteData="routeData" DefaultLayout="typeof(MainLayout)" />
    </Found>
</Router>
```

The Admin host consumes Auth exactly this way — see [Admin host](../Architecture/AdminHost.md).

Auth forms submit to [`AuthController`][8], not `IUserService`:

```text
POST /api/auth/login
POST /api/auth/register
POST /api/auth/logout
GET  /api/auth/me
```

**One API for every role.** Routes never name a role; authorization policies and the caller's role decide access and response shape. The UI on top may differ per audience, the API does not.

Admin user management is JSON-only and owned by [`UsersController`][21]:

| Method | Endpoint | Result |
|---|---|---|
| `GET` | `/api/users` | All users as `AdminUserDto[]` |
| `GET` | `/api/users/{id}` | One `AdminUserDto`; `404` when unknown |
| `GET` | `/api/users/query?page=&pageSize=&search=&role=` | Paged `AdminUserDto` results |
| `POST` | `/api/users` | Create or update a user; `404` for an unknown update id |
| `DELETE` | `/api/users/{id}` | `204`; `404` when unknown; `400` for self-delete |

All `/api/users` endpoints require `AuthClaims.CompanyAdminPolicy`: only an admin can add staff or assign roles. Public registration always creates `Client` users — role escalation is impossible outside the admin API. A duplicate email on `POST /api/users` returns `409` with `{ "error": "A user with this email already exists." }`.

`IUserService` remains responsible for user records. `IAuthService` validates credentials and registration rules; the controller owns HTTP cookies and responses.

**DTO selection follows the user's role.** `GET /api/auth/me`, JSON login, and JSON register return [`AdminUserDto`][9] for `Admin` users and [`ClientUserDto`][10] for everyone else. Both DTOs expose every current `User` field except `PasswordHash`.

## I want my company site to register and sign in users

Send JSON to the credential endpoints:

```text
POST /api/auth/register   (Content-Type: application/json)
POST /api/auth/login      (Content-Type: application/json)
GET  /api/auth/me
```

The register and login routes are shared with the Admin form flow; the request's `Content-Type` selects the behavior. Form posts (`application/x-www-form-urlencoded`) get the antiforgery-protected redirect flow; `application/json` gets the API flow below. Any other content type returns `415`.

`POST /api/auth/register` (JSON) creates a `Client` user and returns `201` with [`ClientUserDto`][10]. The body is [`RegisterModel`][14]-shaped and requires a `confirmation` field that matches `password`:

```json
{
  "name": "Casey",
  "surname": "Client",
  "email": "casey@example.com",
  "contactNumber": "+27000",
  "password": "client-pass",
  "confirmation": "client-pass"
}
```

A duplicate normalized email returns `409` with `{ "error": "A user with this email already exists." }`. `POST /api/auth/login` (JSON, body `{ "email": ..., "password": ... }`) returns `200` with the same DTO and issues the existing `CompanyHq.Auth` cookie; invalid credentials return `401` with `{ "error": "Invalid email or password." }`. Email verification is not part of registration yet (see `plans/README.md`).

The two JSON credential endpoints deliberately ignore antiforgery tokens. Cross-origin JSON requests require a CORS preflight, and the authentication cookie is `SameSite=Lax`; antiforgery remains required for the service-owned form endpoints.

## I want to protect an endpoint

Require `AuthClaims.CurrentCompanyPolicy` for a signed-in user belonging to the resolved company. Require `AuthClaims.CompanyAdminPolicy` when the user must also have the `Admin` role.

## Verify it worked

```bash
curl -i https://acme.example.com/admin/login
curl -i -b cookies.txt https://acme.example.com/api/auth/me
```

The first response renders the Auth layout. The second returns the current user's safe fields without `PasswordHash` after login.

## Common mistakes

- **Calling `IUserService` from a login form.** Submit to `/api/auth/login`; authentication belongs to `IAuthService`.
- **Putting Auth pages in the Admin project.** Load the Auth assembly so its pages and components remain service-owned.
- **Using the Admin main layout for Auth routes.** Auth pages explicitly use `AuthLayout`; the router's default only applies when a page has no layout.
- **Forgetting `MapControllers`.** `ConfigureServices` registers the controller, but the host must map controller endpoints.
- **Calling `UseCompanyHqPlugins` before company resolution.** The cookie cannot be tenant-bound until `ICompanyContext.Current` is set.
- **Sharing a cookie across domains.** The `company` claim must match the resolved company or authentication is rejected.
- **Adding `UseAuthentication` or `UseRateLimiter` in the host.** The plugin owns them; duplicating the middleware runs it twice.
- **Not forwarding the client IP.** Behind a proxy without `X-Forwarded-For`, every client shares one rate-limit bucket.
- **Omitting `RootNamespace` from a plugin project.** Razor types land in `{Assembly}.*` and the layout cannot be imported by its C# namespace.

[1]: ../../Projects/Services/Auth/AuthPlugin.cs
[2]: ../../Projects/Services/Auth/Services/AuthService.cs
[3]: ../../Projects/Abstractions/Users/IUserService.cs
[4]: ../../Projects/Abstractions/Users/ICurrentUser.cs
[5]: ../../Projects/Services/Auth/Pages/Login.razor
[6]: ../../Projects/Services/Auth/Pages/Register.razor
[7]: ../../Projects/Services/Auth/Layout/AuthLayout.razor
[8]: ../../Projects/Services/Auth/Api/AuthController.cs
[9]: ../../Projects/Services/Auth/Dtos/AdminUserDto.cs
[10]: ../../Projects/Services/Auth/Dtos/ClientUserDto.cs
[11]: ../../Projects/Services/Auth/Components/LoginForm.razor
[12]: ../../Projects/Services/Auth/Components/RegisterForm.razor
[13]: ../../Projects/Services/Auth/Models/LoginModel.cs
[14]: ../../Projects/Services/Auth/Models/RegisterModel.cs
[15]: ../../Projects/Services/Auth/Services/UserService.cs
[16]: ../../Projects/Services/Auth/Services/Authorization/CurrentCompanyCookieEvents.cs
[17]: ../../Projects/Services/Auth/Services/Authorization/CurrentCompanyRequirement.cs
[18]: ../../Projects/Services/Auth/Services/Authorization/AuthClaims.cs
[19]: ../../Projects/Services/Auth/Auth.csproj
[20]: ../../Projects/Abstractions/Users/PasswordPolicy.cs
[21]: ../../Projects/Services/Auth/Api/UsersController.cs
[22]: ../../Projects/Abstractions/ICompanyHqPlugin.cs
