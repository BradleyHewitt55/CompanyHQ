using System.Security.Claims;
using System.Text;

using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

using CompanyHQ.Services.Auth.Contracts.Models;
using CompanyHQ.Services.Auth.Services;
using CompanyHQ.Services.Postgres.Services;
using CompanyHQ.Services.Postgres.Contracts;
using CompanyHQ.Services.Users.Contracts.Models;
using CompanyHQ.Services.Auth.Contracts.Identity;

namespace CompanyHQ.Services.Auth.Extensions;

public static class AuthServiceCollectionExtensions
{
    /// <summary>
    /// Registers JWT bearer authentication, the token and auth services, and the auth controller.
    /// Call after <c>AddPostgres</c> and <c>AddUsers</c> — this service verifies passwords with the
    /// User service's <c>IPasswordHasher&lt;User&gt;</c>.
    /// </summary>
    /// <remarks>
    /// The host still has to call <c>app.UseAuthentication()</c> before <c>app.UseAuthorization()</c>;
    /// without it every <c>[Authorize]</c> endpoint sees an anonymous caller.
    /// </remarks>
    public static IServiceCollection AddAuth(this IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        var options = configuration.GetSection(JwtOptions.SectionName).Get<JwtOptions>() ?? new();
        var rateLimitOptions = configuration.GetSection(LoginRateLimitOptions.SectionName).Get<LoginRateLimitOptions>() ?? new();
        var cleanupOptions = configuration.GetSection(RefreshTokenCleanupOptions.SectionName).Get<RefreshTokenCleanupOptions>() ?? new();
        var tokenOptions = configuration.GetSection(AuthTokenOptions.SectionName).Get<AuthTokenOptions>() ?? new();

        // Fail during startup rather than on the first login, where the failure is a 500.
        options.Validate();
        rateLimitOptions.Validate();
        cleanupOptions.Validate();
        tokenOptions.Validate();

        services.AddSingleton(options);
        services.AddSingleton(rateLimitOptions);
        services.AddSingleton(cleanupOptions);
        services.AddSingleton(tokenOptions);
        services.AddScoped<ITokenService, JwtTokenService>();
        services.AddScoped<IAuthTokenService, AuthTokenService>();
        services.AddScoped<IAuthRateLimiter, RedisAuthRateLimiter>();
        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<RefreshTokenCleanupProcessor>();
        services.AddHostedService<RefreshTokenCleanupWorker>();

        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(bearer =>
            {
                bearer.MapInboundClaims = false;
                bearer.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidIssuer = options.Issuer,
                    ValidateAudience = true,
                    ValidAudience = options.Audience,
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(options.SigningKey)),
                    ValidateLifetime = true,
                    ClockSkew = TimeSpan.FromSeconds(options.ClockSkewSeconds),
                    NameClaimType = AuthClaims.Subject,
                    // [Authorize(Roles = "Admin")] reads this claim; the default expects the long
                    // WS-Federation URI, which unmapped tokens do not carry.
                    RoleClaimType = AuthClaims.Role
                };
                bearer.Events = new JwtBearerEvents
                {
                    OnMessageReceived = context =>
                    {
                        if (string.IsNullOrEmpty(context.Token)
                            && context.Request.Path.StartsWithSegments(AuthHubs.PathPrefix)
                            && context.Request.Query.TryGetValue("access_token", out var token))
                        {
                            context.Token = token;
                        }

                        return Task.CompletedTask;
                    },
                    OnTokenValidated = async validationContext =>
                    {
                        var subject = validationContext.Principal?.FindFirstValue(AuthClaims.Subject);
                        if (!Guid.TryParse(subject, out var userId))
                        {
                            validationContext.Fail("The token subject is invalid.");
                            return;
                        }

                        var users = validationContext.HttpContext.RequestServices
                            .GetRequiredService<IPostgresService<User>>();
                        var user = await users.Query()
                            .Where(candidate => candidate.Id == userId)
                            .Select(candidate => new
                            {
                                candidate.Email,
                                candidate.FirstName,
                                candidate.LastName,
                                candidate.Role,
                                candidate.AccessRoleId,
                                RoleName = candidate.AccessRole == null ? null : candidate.AccessRole.Name,
                                RolePermissions = candidate.AccessRole == null ? null : candidate.AccessRole.Permissions,
                                candidate.EmailVerified,
                                candidate.SessionsRevokedAt
                            })
                            .FirstOrDefaultAsync(validationContext.HttpContext.RequestAborted);

                        if (user is null)
                        {
                            validationContext.Fail("The account no longer exists.");
                            return;
                        }

                        // "Sign out everywhere" cannot revoke the access token itself, so it stamps
                        // this instead: a token minted before the panic fails validation on its very
                        // next use, even though its signature is still perfectly good.
                        if (user.SessionsRevokedAt is { } revokedAt)
                        {
                            var issuedAt = validationContext.Principal?.FindFirstValue(JwtRegisteredClaimNames.Iat);
                            if (!long.TryParse(issuedAt, out var issuedAtSeconds) ||
                                DateTimeOffset.FromUnixTimeSeconds(issuedAtSeconds) < revokedAt)
                            {
                                validationContext.Fail("The session was revoked.");
                                return;
                            }
                        }

                        if (validationContext.Principal?.Identity is not ClaimsIdentity identity)
                        {
                            validationContext.Fail("The token identity is invalid.");
                            return;
                        }

                        ReplaceClaim(identity, AuthClaims.Role,
                            user.AccessRoleId is null ? user.Role.ToString()
                            : user.Role == CompanyHQ.Services.Users.Contracts.Enums.UserRole.Admin && user.RoleName == "Admin" ? "Admin" : "Client");
                        foreach (var claim in identity.FindAll(AuthClaims.Permission).ToArray()) identity.RemoveClaim(claim);
                        var permissions = user.AccessRoleId is null
                            ? user.Role == CompanyHQ.Services.Users.Contracts.Enums.UserRole.Admin ? AccessPermissions.All : AccessPermissions.Client
                            : user.RolePermissions ?? [];
                        identity.AddClaims(permissions.Select(permission => new Claim(AuthClaims.Permission, permission)));
                        ReplaceClaim(identity, AuthClaims.Email, user.Email);
                        ReplaceClaim(identity, AuthClaims.FirstName, user.FirstName ?? string.Empty);
                        ReplaceClaim(identity, AuthClaims.LastName, user.LastName ?? string.Empty);
                        ReplaceClaim(identity, AuthClaims.EmailVerified, user.EmailVerified ? "true" : "false");
                    }
                };
            });

        services.AddAuthorization(options =>
        {
            foreach (var permission in AccessPermissions.All)
                options.AddPolicy(permission, policy => policy.RequireAuthenticatedUser()
                    .RequireClaim(AuthClaims.Permission, permission));
            options.AddPolicy(
                AuthPolicies.RequireVerifiedEmail,
                policy => policy.RequireClaim(AuthClaims.EmailVerified, "true"));
        });

        // Controllers live in this assembly, so the host has to be told to look in it.
        services.AddControllers().AddApplicationPart(typeof(AuthServiceCollectionExtensions).Assembly);

        return services;
    }

    private static void ReplaceClaim(ClaimsIdentity identity, string claimType, string value)
    {
        foreach (var claim in identity.FindAll(claimType).ToArray())
        {
            identity.RemoveClaim(claim);
        }

        identity.AddClaim(new Claim(claimType, value));
    }
}
