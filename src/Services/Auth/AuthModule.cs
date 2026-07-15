using System.Security.Claims;
using Services.Companies.Contracts.Services;
using Services.Auth.Models;
using Services.Auth.Pages;
using Services.Auth.Services;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Services.Auth;

public sealed class AuthModule : IServiceModule
{
    private const string IssuedClaim = "companyhq:issued";
    public ServiceManifest Manifest { get; } = ServiceManifest.Load(typeof(AuthModule).Assembly);
    public IReadOnlyList<ServiceComponentRegistration> Components => [new("users", typeof(UsersPage))];

    public void Configure(IServiceCollection services)
    {
        services.AddAuthentication(IdentityConstants.ApplicationScheme).AddIdentityCookies();
        services.AddAuthorization(options =>
        {
            options.FallbackPolicy = new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build();
            options.AddPolicy("ManageUsers", policy => policy.RequireRole(AuthRoles.Owner, AuthRoles.Admin));
            options.AddPolicy("Contribute", policy => policy.RequireRole(AuthRoles.Member, AuthRoles.Admin, AuthRoles.Owner));
            options.AddPolicy("OwnerOnly", policy => policy.RequireRole(AuthRoles.Owner));
        });
        services.AddIdentityCore<CompanyUser>(options =>
        {
            options.Password.RequiredLength = 12;
            options.Password.RequireDigit = true;
            options.Password.RequireLowercase = true;
            options.Password.RequireUppercase = true;
            options.Password.RequireNonAlphanumeric = true;
            options.Lockout.AllowedForNewUsers = true;
            options.Lockout.MaxFailedAccessAttempts = 5;
            options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(5);
            options.User.RequireUniqueEmail = true;
        })
        .AddRoles<IdentityRole<Guid>>()
        .AddSignInManager()
        .AddDefaultTokenProviders()
        .AddPasswordValidator<CommonPasswordValidator>();
        services.Configure<SecurityStampValidatorOptions>(options => options.ValidationInterval = TimeSpan.Zero);
        services.ConfigureApplicationCookie(options =>
        {
            options.Cookie.Name = "CompanyHQ.Auth";
            options.Cookie.HttpOnly = true;
            options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
            options.Cookie.SameSite = SameSiteMode.Lax;
            options.ExpireTimeSpan = TimeSpan.FromHours(8);
            options.SlidingExpiration = true;
            options.LoginPath = "/auth/login";
            options.AccessDeniedPath = "/auth/denied";
            options.Events.OnSigningIn = context =>
            {
                (context.Principal?.Identity as ClaimsIdentity)?.AddClaim(new Claim(IssuedClaim, DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString()));
                return Task.CompletedTask;
            };
            options.Events.OnValidatePrincipal = async context =>
            {
                await SecurityStampValidator.ValidatePrincipalAsync(context);
                var issued = context.Principal?.FindFirstValue(IssuedClaim);
                if (long.TryParse(issued, out var seconds) && DateTimeOffset.UtcNow - DateTimeOffset.FromUnixTimeSeconds(seconds) > TimeSpan.FromHours(24))
                    context.RejectPrincipal();
            };
            options.Events.OnRedirectToLogin = context =>
            {
                if (context.Request.Path.StartsWithSegments("/api")) context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                else context.Response.Redirect(context.RedirectUri);
                return Task.CompletedTask;
            };
            options.Events.OnRedirectToAccessDenied = context =>
            {
                if (context.Request.Path.StartsWithSegments("/api")) context.Response.StatusCode = StatusCodes.Status403Forbidden;
                else context.Response.Redirect(context.RedirectUri);
                return Task.CompletedTask;
            };
        });
        services.AddScoped<AdminUserSeederService>();
        services.AddScoped<IUserDirectoryService, UserDirectoryService>();
        services.AddSingleton<LoginRateGateService>();
        services.AddHttpContextAccessor();
    }

    public void MapEndpoints(IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/auth/login", (string? returnUrl) => new RazorComponentResult<LoginPage>(new Dictionary<string, object?> { [nameof(LoginPage.ReturnUrl)] = returnUrl })).AllowAnonymous();
        endpoints.MapPost("/auth/login", async (HttpContext context, SignInManager<CompanyUser> signIn, UserManager<CompanyUser> users, LoginRateGateService rateGate, ILogger<AuthModule> logger) =>
        {
            if (context.Features.Get<IAntiforgeryValidationFeature>()?.IsValid == false) return Results.BadRequest();
            var form = await context.Request.ReadFormAsync();
            var username = form["Username"].ToString();
            var returnUrl = form["ReturnUrl"].ToString();
            var usernameKey = $"user:{username}";
            var ipKey = $"ip:{context.Connection.RemoteIpAddress}";
            if (rateGate.IsLimited(usernameKey, 5) || rateGate.IsLimited(ipKey, 20))
            {
                var candidate = await users.FindByNameAsync(username);
                if (candidate is null || !await users.CheckPasswordAsync(candidate, form["Password"].ToString()))
                {
                    logger.LogWarning("Sign-in rate limited for user {Username} from {RemoteIpAddress}", username, context.Connection.RemoteIpAddress);
                    return Results.StatusCode(StatusCodes.Status429TooManyRequests);
                }
            }
            var result = await signIn.PasswordSignInAsync(username, form["Password"].ToString(), true, true);
            if (!result.Succeeded)
            {
                rateGate.Fail(usernameKey); rateGate.Fail(ipKey);
                logger.LogWarning("Sign-in failed for user {Username} from {RemoteIpAddress}; locked out: {LockedOut}", username, context.Connection.RemoteIpAddress, result.IsLockedOut);
                return Results.Text("Invalid username or password.", statusCode: StatusCodes.Status401Unauthorized);
            }
            rateGate.Succeed(usernameKey);
            logger.LogInformation("Sign-in succeeded for user {Username} from {RemoteIpAddress}", username, context.Connection.RemoteIpAddress);
            return Results.LocalRedirect(IsLocal(returnUrl) ? returnUrl : "/admin/dashboard");
        }).AllowAnonymous().WithMetadata(new RequireAntiforgeryTokenAttribute());

        endpoints.MapPost("/auth/logout", async (HttpContext context, ClaimsPrincipal principal, UserManager<CompanyUser> users, SignInManager<CompanyUser> signIn) =>
        {
            if (context.Features.Get<IAntiforgeryValidationFeature>()?.IsValid == false) return Results.BadRequest();
            var user = await users.GetUserAsync(principal);
            if (user is not null) await users.UpdateSecurityStampAsync(user);
            await signIn.SignOutAsync();
            return Results.Redirect("/auth/login");
        }).RequireAuthorization().WithMetadata(new RequireAntiforgeryTokenAttribute());

        endpoints.MapPost("/api/account/password", async (HttpContext context, ClaimsPrincipal principal, UserManager<CompanyUser> users, SignInManager<CompanyUser> signIn) =>
        {
            if (context.Features.Get<IAntiforgeryValidationFeature>()?.IsValid == false) return Results.BadRequest();
            var form = await context.Request.ReadFormAsync();
            var user = await users.GetUserAsync(principal);
            if (user is null) return Results.Unauthorized();
            var result = await users.ChangePasswordAsync(user, form["CurrentPassword"].ToString(), form["NewPassword"].ToString());
            if (!result.Succeeded) return Results.BadRequest(result.Errors.Select(error => error.Description));
            await users.UpdateSecurityStampAsync(user);
            await signIn.RefreshSignInAsync(user);
            return Results.NoContent();
        }).RequireAuthorization().WithMetadata(new RequireAntiforgeryTokenAttribute());

        endpoints.MapGet("/auth/denied", () => Results.StatusCode(StatusCodes.Status403Forbidden)).AllowAnonymous();
        endpoints.MapPost("/internal/bootstrap/owner", async (HttpContext context, string username, AdminUserSeederService seeder) =>
        {
            var configuration = context.RequestServices.GetRequiredService<Microsoft.Extensions.Configuration.IConfiguration>();
            var marker = Path.Combine(configuration["DataProtection:Path"] ?? AppContext.BaseDirectory, ".bootstrap-complete");
            if (File.Exists(marker)) return Results.StatusCode(StatusCodes.Status410Gone);
            if (!configuration.GetValue<bool>("Bootstrap:Enabled") || !context.Request.Headers.TryGetValue("X-Bootstrap-Token", out var token) || token != configuration["Bootstrap:Token"])
                return Results.Unauthorized();
            var owner = await seeder.SeedOwner(username);
            if (owner.Created)
            {
                Directory.CreateDirectory(Path.GetDirectoryName(marker)!);
                await File.WriteAllTextAsync(marker, DateTimeOffset.UtcNow.ToString("O"), context.RequestAborted);
            }
            else return Results.StatusCode(StatusCodes.Status410Gone);
            return Results.Ok(new { owner.Username, owner.Password, owner.Created });
        }).AllowAnonymous();
        endpoints.MapGet("/api/admin/users", async (IUserDirectoryService users, CancellationToken cancellationToken) => Results.Ok(await users.GetUsers(cancellationToken))).RequireAuthorization();
        endpoints.MapPut("/api/admin/users/{id:guid}/role", ChangeRole).RequireAuthorization("ManageUsers").WithMetadata(new RequireAntiforgeryTokenAttribute());
        endpoints.MapDelete("/api/admin/users/{id:guid}", DeleteUser).RequireAuthorization("ManageUsers").WithMetadata(new RequireAntiforgeryTokenAttribute());
    }

    private static async Task<IResult> ChangeRole(HttpContext context, Guid id, [FromQuery] string role, UserManager<CompanyUser> users, ILogger<AuthModule> logger)
    {
        if (context.Features.Get<IAntiforgeryValidationFeature>()?.IsValid == false) return Results.BadRequest();
        if (!AuthRoles.All.Contains(role, StringComparer.OrdinalIgnoreCase)) return Results.BadRequest();
        var user = await users.FindByIdAsync(id.ToString());
        if (user is null) return Results.NotFound();
        var current = await users.GetRolesAsync(user);
        if (current.Contains(AuthRoles.Owner) && !role.Equals(AuthRoles.Owner, StringComparison.OrdinalIgnoreCase) && await CountOwners(users) == 1)
            return Results.Conflict(new { error = "The last Owner cannot be demoted." });
        await users.RemoveFromRolesAsync(user, current);
        var result = await users.AddToRoleAsync(user, role);
        if (result.Succeeded) logger.LogInformation("Role changed for user {UserId} from {PreviousRoles} to {Role} by {Actor}", id, current, role, context.User.Identity?.Name);
        return result.Succeeded ? Results.NoContent() : Results.BadRequest(result.Errors.Select(error => error.Description));
    }

    private static async Task<IResult> DeleteUser(HttpContext context, Guid id, UserManager<CompanyUser> users, ILogger<AuthModule> logger)
    {
        if (context.Features.Get<IAntiforgeryValidationFeature>()?.IsValid == false) return Results.BadRequest();
        var user = await users.FindByIdAsync(id.ToString());
        if (user is null) return Results.NotFound();
        if (await users.IsInRoleAsync(user, AuthRoles.Owner) && await CountOwners(users) == 1)
            return Results.Conflict(new { error = "The last Owner cannot be deleted." });
        var deleted = await users.DeleteAsync(user);
        if (deleted.Succeeded) logger.LogInformation("User {UserId} deleted by {Actor}", id, context.User.Identity?.Name);
        return deleted.Succeeded ? Results.NoContent() : Results.BadRequest();
    }

    private static async Task<int> CountOwners(UserManager<CompanyUser> users) => (await users.GetUsersInRoleAsync(AuthRoles.Owner)).Count;
    private static bool IsLocal(string? url) => !string.IsNullOrWhiteSpace(url) && url[0] == '/' && (url.Length == 1 || (url[1] != '/' && url[1] != '\\'));
    public sealed record LoginForm(string Username, string Password, string? ReturnUrl);
    public sealed record PasswordChangeForm(string CurrentPassword, string NewPassword);
}
