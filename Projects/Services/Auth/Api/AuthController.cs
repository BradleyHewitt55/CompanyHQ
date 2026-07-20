using System.Security.Claims;
using Abstractions.Companies;
using Abstractions.Data;
using Abstractions.Users;
using CompanyHQ.Services.Auth.Dtos;
using CompanyHQ.Services.Auth.Services.Authorization;
using CompanyHQ.Services.Auth.Models;
using CompanyHQ.Services.Auth.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace CompanyHQ.Services.Auth.Api;

[ApiController]
[Route("api/auth")]
public sealed class AuthController(
    IAuthService auth,
    ICurrentUser currentUser,
    ICompanyContext companyContext,
    ICompanyDatabases databases) : ControllerBase
{
    private const string DuplicateEmailMessage = "A user with this email already exists.";

    // The form and JSON credential actions share one route each; the request's
    // Content-Type picks the action, so browser forms get redirects and any other
    // frontend gets JSON from the same URL.
    [AllowAnonymous]
    [HttpPost("login")]
    [ValidateAntiForgeryToken]
    [Consumes("application/x-www-form-urlencoded", "multipart/form-data")]
    [EnableRateLimiting(AuthClaims.CredentialRateLimitPolicy)]
    public async Task<IActionResult> Login(
        [FromForm] LoginModel login,
        CancellationToken cancellationToken)
    {
        if (companyContext.Current is null)
        {
            return LoginFailure("No company is configured for this domain.");
        }

        var user = await auth.LoginAsync(login, cancellationToken);
        if (user is null)
        {
            return LoginFailure("Invalid email or password.");
        }

        await HttpContext.SignInAsync(
            CookieAuthenticationDefaults.AuthenticationScheme,
            CreatePrincipal(user, companyContext.Current),
            new AuthenticationProperties { IsPersistent = true });
        return LocalRedirect("/admin");
    }

    [AllowAnonymous]
    [HttpPost("register")]
    [ValidateAntiForgeryToken]
    [Consumes("application/x-www-form-urlencoded", "multipart/form-data")]
    [EnableRateLimiting(AuthClaims.CredentialRateLimitPolicy)]
    public async Task<IActionResult> Register(
        [FromForm] RegisterModel registration,
        CancellationToken cancellationToken)
    {
        if (companyContext.Current is null
            || !databases.IsConfigured(companyContext.Current))
        {
            return RegisterFailure("The company database is not configured.");
        }

        try
        {
            var user = await auth.RegisterAsync(registration, cancellationToken);
            return user is null
                ? RegisterFailure("An administrator already exists.")
                : LocalRedirect("/admin/login");
        }
        catch (UniqueConstraintException)
        {
            return RegisterFailure(DuplicateEmailMessage);
        }
    }

    [AllowAnonymous]
    [HttpPost("register")]
    [IgnoreAntiforgeryToken]
    [Consumes("application/json")]
    [EnableRateLimiting(AuthClaims.CredentialRateLimitPolicy)]
    public async Task<IActionResult> RegisterClient(
        [FromBody] RegisterModel registration,
        CancellationToken cancellationToken)
    {
        if (companyContext.Current is null
            || !databases.IsConfigured(companyContext.Current))
        {
            return BadRequest(new { error = "The company database is not configured." });
        }

        try
        {
            var user = await auth.RegisterClientAsync(registration, cancellationToken);
            return StatusCode(StatusCodes.Status201Created, ToRoleDto(user!));
        }
        catch (UniqueConstraintException)
        {
            return Conflict(new { error = DuplicateEmailMessage });
        }
    }

    [AllowAnonymous]
    [HttpPost("login")]
    [IgnoreAntiforgeryToken]
    [Consumes("application/json")]
    [EnableRateLimiting(AuthClaims.CredentialRateLimitPolicy)]
    public async Task<IActionResult> LoginClient(
        [FromBody] LoginModel login,
        CancellationToken cancellationToken)
    {
        if (companyContext.Current is null)
        {
            return Unauthorized(new { error = "Invalid email or password." });
        }

        var user = await auth.LoginAsync(login, cancellationToken);
        if (user is null)
        {
            return Unauthorized(new { error = "Invalid email or password." });
        }

        await HttpContext.SignInAsync(
            CookieAuthenticationDefaults.AuthenticationScheme,
            CreatePrincipal(user, companyContext.Current),
            new AuthenticationProperties { IsPersistent = true });
        return Ok(ToRoleDto(user));
    }

    [Authorize(Policy = AuthClaims.CurrentCompanyPolicy)]
    [HttpPost("logout")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Logout()
    {
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        return LocalRedirect("/admin/login");
    }

    [Authorize(Policy = AuthClaims.CurrentCompanyPolicy)]
    [HttpGet("me")]
    public IActionResult Me()
    {
        var user = currentUser.User;
        return user is null ? Unauthorized() : Ok(ToRoleDto(user));
    }

    private IActionResult LoginFailure(string message)
        => LocalRedirect($"/admin/login?error={Uri.EscapeDataString(message)}");

    private IActionResult RegisterFailure(string message)
        => LocalRedirect($"/admin/setup-user?error={Uri.EscapeDataString(message)}");

    // One API for every role: the route never names a role, the caller's role
    // picks the contract. Admins get the admin DTO, everyone else the client DTO.
    private static object ToRoleDto(User user)
        => user.Role == Roles.Admin ? AdminUserDto.From(user) : ClientUserDto.From(user);

    private static ClaimsPrincipal CreatePrincipal(User user, string company)
    {
        Claim[] claims =
        [
            new(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new(ClaimTypes.Name, $"{user.Name} {user.Surname}"),
            new(ClaimTypes.Email, user.Email),
            new(ClaimTypes.Role, user.Role),
            new(AuthClaims.Company, company),
        ];
        return new ClaimsPrincipal(
            new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme));
    }
}
