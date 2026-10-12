using System.Net.Http.Headers;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Encodings.Web;

using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using CompanyHQ.Services.Mcp.Contracts.Models;
using CompanyHQ.Services.Postgres.Contracts;
using CompanyHQ.Services.Persistence.Services;
using CompanyHQ.Services.Users.Contracts.Models;
using CompanyHQ.Services.Auth.Contracts.Identity;

namespace CompanyHQ.Services.Mcp.Authentication;

/// <summary>
/// Turns a <c>pgk_</c> agent key into the same shape of <see cref="ClaimsPrincipal"/> a JWT produces —
/// role and email read from Postgres on every call, never trusted from the key. Registered under its
/// own scheme name, never the host default, so nothing outside <c>/mcp</c> ever evaluates a request
/// against it: an agent key presented anywhere else simply never reaches this handler.
/// </summary>
public sealed class AgentKeyAuthenticationHandler(
    IOptionsMonitor<AgentKeyAuthenticationSchemeOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder,
    IRepository<AgentKey> keys,
    IRepository<User> users,
    IRepository<AccessRole> roles,
    TimeProvider clock,
    McpOAuthOptions oauthOptions)
    : AuthenticationHandler<AgentKeyAuthenticationSchemeOptions>(options, logger, encoder)
{
    public const string SchemeName = "AgentKey";

    private const string KeyPrefix = "pgk_";
    private const string OAuthTokenPrefix = "pgo_";

    /// <summary>Throttles the busiest agent from becoming the busiest writer in the database.</summary>
    private static readonly TimeSpan LastUsedResolution = TimeSpan.FromMinutes(1);

    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!Request.Headers.TryGetValue("Authorization", out var header) ||
            !AuthenticationHeaderValue.TryParse(header.ToString(), out var authorization) ||
            !string.Equals(authorization.Scheme, "Bearer", StringComparison.OrdinalIgnoreCase) ||
            string.IsNullOrWhiteSpace(authorization.Parameter))
        {
            return AuthenticateResult.NoResult();
        }

        var presented = authorization.Parameter;
        var isOAuth = presented.StartsWith(OAuthTokenPrefix, StringComparison.Ordinal);
        if (!isOAuth && !presented.StartsWith(KeyPrefix, StringComparison.Ordinal))
        {
            return AuthenticateResult.NoResult();
        }

        var hash = Hash(presented);
        var now = clock.GetUtcNow();

        var key = await keys.Query()
            .FirstOrDefaultAsync(candidate => candidate.KeyHash == hash, Context.RequestAborted);

        if (key is null || !key.IsActive(now) || key.IsOAuth != isOAuth)
        {
            return AuthenticateResult.Fail("Unknown, revoked, or expired agent key.");
        }

        if (isOAuth && !string.Equals(key.OAuthResource, oauthOptions.ResourceAddress, StringComparison.Ordinal))
        {
            return AuthenticateResult.Fail("The access token is for a different resource.");
        }

        var account = await users.Query()
                .Where(candidate => candidate.Id == key.UserId)
            .Select(candidate => new
            {
                candidate.Email,
                candidate.FirstName,
                candidate.LastName,
                candidate.Role,
                candidate.AccessRoleId,
                OwnerPermissions = candidate.AccessRole == null ? null : candidate.AccessRole.Permissions,
                candidate.McpDisabledTools
            })
            .FirstOrDefaultAsync(Context.RequestAborted);

        if (account is null)
        {
            return AuthenticateResult.Fail("The linked account no longer exists.");
        }

        var assignedRoleId = key.AccessRoleId ?? account.AccessRoleId;
        var assignedRole = assignedRoleId is { } roleId
            ? await roles.Query()
                .Where(role => role.Id == roleId)
                .Select(role => new { role.Name, role.Permissions })
                .FirstOrDefaultAsync(Context.RequestAborted)
            : null;
        if (assignedRoleId is not null && assignedRole is null)
            return AuthenticateResult.Fail("The assigned role no longer exists.");

        if (key.LastUsedAt is null || now - key.LastUsedAt.Value >= LastUsedResolution)
        {
            key.LastUsedAt = now;
            await keys.UpdateAsync(key, Context.RequestAborted);
        }

        var claims = new List<Claim>
        {
            new(AuthClaims.Subject, key.UserId.ToString()),
            new(AuthClaims.Email, account.Email),
            new(AuthClaims.FirstName, account.FirstName ?? string.Empty),
            new(AuthClaims.LastName, account.LastName ?? string.Empty),
            new(AuthClaims.Role, assignedRoleId is null ? account.Role.ToString()
                : account.Role == CompanyHQ.Services.Users.Contracts.Enums.UserRole.Admin && assignedRole!.Name == "Admin" ? "Admin" : "Client"),
            new(McpClaims.Agent, "true"),
            new(McpClaims.AgentKeyId, key.Id.ToString()),
            new(McpClaims.AgentKeyAccess, key.Access.ToString())
        };
        var ownerPermissions = account.AccessRoleId is null
            ? account.Role == CompanyHQ.Services.Users.Contracts.Enums.UserRole.Admin
                ? AccessPermissions.All : AccessPermissions.Client
            : account.OwnerPermissions ?? [];
        var permissions = assignedRole is not null
            ? assignedRole.Permissions.Intersect(ownerPermissions, StringComparer.Ordinal)
            : ownerPermissions;
        claims.AddRange(permissions.Select(permission => new Claim(AuthClaims.Permission, permission)));
        claims.AddRange(account.McpDisabledTools
            .Concat(key.McpDisabledTools)
            .Distinct(StringComparer.Ordinal)
            .Select(capability => new Claim(McpClaims.DisabledCapability, capability)));

        var identity = new ClaimsIdentity(
            claims,
            SchemeName,
            AuthClaims.Subject,
            AuthClaims.Role);

        var ticket = new AuthenticationTicket(new ClaimsPrincipal(identity), SchemeName);
        return AuthenticateResult.Success(ticket);
    }

    protected override Task HandleChallengeAsync(AuthenticationProperties properties)
    {
        Response.StatusCode = StatusCodes.Status401Unauthorized;
        Response.Headers.WWWAuthenticate =
            $"Bearer resource_metadata=\"{oauthOptions.ProtectedResourceMetadataEndpoint}\"";
        return Task.CompletedTask;
    }

    private static string Hash(string value) =>
        Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(value)));
}
