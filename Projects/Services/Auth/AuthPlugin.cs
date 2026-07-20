using System.Reflection;
using System.Threading.RateLimiting;
using Abstractions.Plugins;
using Abstractions.Users;
using CompanyHQ.Services.Auth.Api;
using CompanyHQ.Services.Auth.Services.Authorization;
using CompanyHQ.Services.Auth.Services;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace CompanyHQ.Services.Auth;

public sealed class AuthPlugin : ICompanyHqPlugin
{
    public PluginDescriptor Descriptor { get; } = new(
        "companyhq.auth",
        "Auth",
        "Users, sessions, roles, and company-bound authorization.",
        new Version(1, 0, 0),
        PluginOrigin.BuiltIn,
        PluginActivation.Required,
        ["companyhq.companies", "companyhq.postgress"]);

    public void ConfigureServices(IServiceCollection services, IConfiguration configuration)
    {
        services.AddScoped<IUserService, UserService>();
        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<ICurrentUser, CurrentUser>();
        services.AddScoped<IPasswordHasher<User>, PasswordHasher<User>>();
        services.AddScoped<CurrentCompanyCookieEvents>();
        services.AddScoped<IAuthorizationHandler, CurrentCompanyHandler>();
        services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
            .AddCookie(options =>
            {
                options.LoginPath = "/admin/login";
                options.AccessDeniedPath = "/admin/login";
                options.EventsType = typeof(CurrentCompanyCookieEvents);
                options.Cookie.Name = "CompanyHq.Auth";
                options.Cookie.HttpOnly = true;
                options.Cookie.SameSite = SameSiteMode.Lax;
                // TLS is always terminated in front of the app (dev included via
                // https redirection), so never let the flag degrade to SameAsRequest.
                options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
                options.ExpireTimeSpan = TimeSpan.FromHours(12);
                options.SlidingExpiration = true;
            });
        // Credential endpoints are the brute-force surface; everything else stays
        // unthrottled. Hosts must call UseRateLimiter for the policy to apply.
        services.AddRateLimiter(limiter =>
        {
            limiter.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            limiter.AddPolicy(AuthClaims.CredentialRateLimitPolicy, httpContext =>
                RateLimitPartition.GetFixedWindowLimiter(
                    httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                    _ => new FixedWindowRateLimiterOptions
                    {
                        PermitLimit = 5,
                        Window = TimeSpan.FromMinutes(1),
                    }));
        });
        services.AddControllersWithViews().AddApplicationPart(typeof(AuthController).Assembly);
        services.AddAuthorization(options =>
        {
            options.AddPolicy(AuthClaims.CurrentCompanyPolicy,
                policy => policy.RequireAuthenticatedUser().AddRequirements(new CurrentCompanyRequirement()));
            options.AddPolicy(AuthClaims.CompanyAdminPolicy,
                policy => policy.RequireAuthenticatedUser()
                    .RequireRole(Roles.Admin)
                    .AddRequirements(new CurrentCompanyRequirement()));
        });
    }

    // The rate limiter, authentication, and authorization middleware belong to this
    // plugin's registrations; hosts previously had to know that and re-declare it.
    public void ConfigureApplication(IApplicationBuilder application)
    {
        application.UseRateLimiter();
        application.UseAuthentication();
        application.UseAuthorization();
    }

    public IEnumerable<Assembly> RouteAssemblies => [typeof(AuthPlugin).Assembly];
}
