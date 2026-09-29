using System.Security.Cryptography;
using System.Text;

using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.WebUtilities;

using CompanyHQ.Services.Mcp.Contracts.Dtos;
using CompanyHQ.Services.Mcp.Contracts.Enums;
using CompanyHQ.Services.Mcp.Contracts.Forms;
using CompanyHQ.Services.Mcp.Contracts.Models;
using CompanyHQ.Services.Postgres.Data;

namespace CompanyHQ.Services.Mcp.Services;

public sealed class McpOAuthService(
    PostgresDbContext context,
    McpOAuthOptions options,
    TimeProvider clock) : IMcpOAuthService
{
    private const int SecretBytes = 32;
    private const string AccessTokenPrefix = "pgo_";
    private const string RefreshTokenPrefix = "pgr_";

    public async Task<McpOAuthClientDto> RegisterClientAsync(
        RegisterMcpClientRequest request,
        CancellationToken cancellationToken = default)
    {
        if (request.RedirectUris.Count == 0
            || request.RedirectUris.Any(uri => !options.IsRedirectUriAllowed(uri))
            || (request.GrantTypes is { Count: > 0 }
                && request.GrantTypes.Any(type => type is not ("authorization_code" or "refresh_token")))
            || (request.ResponseTypes is { Count: > 0 }
                && request.ResponseTypes.Any(type => !string.Equals(type, "code", StringComparison.Ordinal)))
            || (request.TokenEndpointAuthMethod is not null
                && !string.Equals(request.TokenEndpointAuthMethod, "none", StringComparison.Ordinal))
            || (request.Scope is not null
                && request.Scope.Split(' ', StringSplitOptions.RemoveEmptyEntries)
                    .Any(scope => scope is not ("mcp" or "offline_access"))))
        {
            throw new ArgumentException("The OAuth client metadata is invalid.", nameof(request));
        }

        var clientId = "pgc_" + WebEncoders.Base64UrlEncode(RandomNumberGenerator.GetBytes(24));
        var client = new McpOAuthClient
        {
            ClientId = clientId,
            RedirectUris = [.. request.RedirectUris],
            GrantTypes = request.GrantTypes is { Count: > 0 }
                ? [.. request.GrantTypes.Distinct(StringComparer.Ordinal)]
                : ["authorization_code", "refresh_token"],
            ClientName = string.IsNullOrWhiteSpace(request.ClientName) ? null : request.ClientName.Trim()
        };
        context.Set<McpOAuthClient>().Add(client);
        await context.SaveChangesAsync(cancellationToken);

        return new McpOAuthClientDto(
            client.ClientId,
            client.CreatedAt.ToUnixTimeSeconds(),
            client.RedirectUris,
            client.GrantTypes,
            ["code"],
            "none",
            client.ClientName);
    }

    public async Task<string?> IssueAuthorizationCodeAsync(
        Guid userId,
        McpAuthorizeRequest request,
        CancellationToken cancellationToken = default)
    {
        var registered = await context.Set<McpOAuthClient>()
            .AsNoTracking()
            .FirstOrDefaultAsync(client => client.ClientId == request.ClientId, cancellationToken);
        if (registered is null
            || !registered.RedirectUris.Any(uri => McpOAuthOptions.RedirectUrisMatch(uri, request.RedirectUri)))
        {
            return null;
        }

        var value = WebEncoders.Base64UrlEncode(RandomNumberGenerator.GetBytes(SecretBytes));
        var code = new McpAuthorizationCode
        {
            UserId = userId,
            CodeHash = Hash(value),
            ClientId = request.ClientId,
            RedirectUri = request.RedirectUri,
            Resource = request.Resource,
            CodeChallenge = request.CodeChallenge,
            ExpiresAt = clock.GetUtcNow() + options.AuthorizationCodeLifetime
        };

        context.Set<McpAuthorizationCode>().Add(code);
        await context.SaveChangesAsync(cancellationToken);
        return value;
    }

    public async Task<McpOAuthTokenResult> RedeemAuthorizationCodeAsync(
        McpTokenRequest request,
        CancellationToken cancellationToken = default)
    {
        var now = clock.GetUtcNow();
        var codeHash = Hash(request.Code);

        // The execution strategy is required because the host enables Npgsql retries. The
        // conditional update makes concurrent redemption single-use even when two requests race.
        return await context.Database.CreateExecutionStrategy().ExecuteAsync(async () =>
        {
            context.ChangeTracker.Clear();
            await using var transaction = await context.Database.BeginTransactionAsync(cancellationToken);

            var stored = await context.Set<McpAuthorizationCode>()
                .AsNoTracking()
                .FirstOrDefaultAsync(code => code.CodeHash == codeHash, cancellationToken);

            if (stored is null || !stored.IsActive(now))
            {
                return McpOAuthTokenResult.Failure("invalid_grant");
            }

            if (!string.Equals(stored.ClientId, request.ClientId, StringComparison.Ordinal)
                || !McpOAuthOptions.RedirectUrisMatch(stored.RedirectUri, request.RedirectUri)
                || !string.Equals(stored.Resource, request.Resource, StringComparison.Ordinal)
                || !VerifyPkce(request.CodeVerifier, stored.CodeChallenge))
            {
                return McpOAuthTokenResult.Failure("invalid_grant");
            }

            var consumed = await context.Set<McpAuthorizationCode>()
                .Where(code => code.Id == stored.Id && code.ConsumedAt == null)
                .ExecuteUpdateAsync(
                    setters => setters.SetProperty(code => code.ConsumedAt, now),
                    cancellationToken);
            if (consumed != 1)
            {
                return McpOAuthTokenResult.Failure("invalid_grant");
            }

            var value = AccessTokenPrefix + WebEncoders.Base64UrlEncode(RandomNumberGenerator.GetBytes(SecretBytes));
            var expiresAt = now + options.AccessTokenLifetime;
            context.Set<AgentKey>().Add(CreateAccessKey(
                value,
                stored.UserId,
                stored.ClientId,
                stored.Resource,
                expiresAt));

            string? refreshValue = null;
            var registered = await context.Set<McpOAuthClient>()
                .AsNoTracking()
                .FirstOrDefaultAsync(client => client.ClientId == stored.ClientId, cancellationToken);
            if (registered?.GrantTypes.Contains("refresh_token", StringComparer.Ordinal) is true)
            {
                refreshValue = RefreshTokenPrefix + WebEncoders.Base64UrlEncode(RandomNumberGenerator.GetBytes(SecretBytes));
                context.Set<McpOAuthRefreshToken>().Add(new McpOAuthRefreshToken
                {
                    UserId = stored.UserId,
                    TokenHash = Hash(refreshValue),
                    ClientId = stored.ClientId,
                    Resource = stored.Resource,
                    ExpiresAt = now + options.RefreshTokenLifetime
                });
            }
            await context.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            return McpOAuthTokenResult.Success(new McpOAuthTokenDto(
                value,
                "Bearer",
                checked((int)Math.Ceiling(options.AccessTokenLifetime.TotalSeconds)),
                stored.Resource,
                refreshValue));
        });
    }

    public async Task<McpOAuthTokenResult> RedeemRefreshTokenAsync(
        McpRefreshTokenRequest request,
        CancellationToken cancellationToken = default)
    {
        var now = clock.GetUtcNow();
        var tokenHash = Hash(request.RefreshToken);

        return await context.Database.CreateExecutionStrategy().ExecuteAsync(async () =>
        {
            context.ChangeTracker.Clear();
            await using var transaction = await context.Database.BeginTransactionAsync(cancellationToken);

            var stored = await context.Set<McpOAuthRefreshToken>()
                .FirstOrDefaultAsync(token => token.TokenHash == tokenHash, cancellationToken);
            var registered = stored is null
                ? null
                : await context.Set<McpOAuthClient>()
                    .AsNoTracking()
                    .FirstOrDefaultAsync(client => client.ClientId == stored.ClientId, cancellationToken);

            if (stored is null
                || registered is null
                || !stored.IsActive(now)
                || !registered.GrantTypes.Contains("refresh_token", StringComparer.Ordinal)
                || !string.Equals(stored.ClientId, request.ClientId, StringComparison.Ordinal)
                || !string.Equals(stored.Resource, request.Resource, StringComparison.Ordinal))
            {
                return McpOAuthTokenResult.Failure("invalid_grant");
            }

            stored.RevokedAt = now;
            var accessValue = AccessTokenPrefix + WebEncoders.Base64UrlEncode(RandomNumberGenerator.GetBytes(SecretBytes));
            var refreshValue = RefreshTokenPrefix + WebEncoders.Base64UrlEncode(RandomNumberGenerator.GetBytes(SecretBytes));
            context.Set<AgentKey>().Add(CreateAccessKey(
                accessValue,
                stored.UserId,
                stored.ClientId,
                stored.Resource,
                now + options.AccessTokenLifetime));
            context.Set<McpOAuthRefreshToken>().Add(new McpOAuthRefreshToken
            {
                UserId = stored.UserId,
                TokenHash = Hash(refreshValue),
                ClientId = stored.ClientId,
                Resource = stored.Resource,
                ExpiresAt = now + options.RefreshTokenLifetime
            });
            await context.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            return McpOAuthTokenResult.Success(new McpOAuthTokenDto(
                accessValue,
                "Bearer",
                checked((int)Math.Ceiling(options.AccessTokenLifetime.TotalSeconds)),
                stored.Resource,
                refreshValue));
        });
    }

    private static AgentKey CreateAccessKey(
        string value,
        Guid userId,
        string clientId,
        string resource,
        DateTimeOffset expiresAt) => new()
        {
            UserId = userId,
            Name = "CompanyHQ OAuth agent",
            KeyHash = Hash(value),
            Prefix = value[..8],
            IsOAuth = true,
            OAuthClientId = clientId,
            OAuthResource = resource,
            // OAuth connections are the default agent connection. They may use the owner-scoped
            // write tools while the service layer still enforces ownership for every mutation.
            Access = AgentKeyAccess.Write,
            ExpiresAt = expiresAt
        };

    private static bool VerifyPkce(string verifier, string expectedChallenge)
    {
        if (verifier.Length is < 43 or > 128
            || verifier.Any(character => character is not (>= 'A' and <= 'Z')
                and not (>= 'a' and <= 'z')
                and not (>= '0' and <= '9')
                and not '-' and not '.' and not '_' and not '~'))
        {
            return false;
        }

        var computed = WebEncoders.Base64UrlEncode(SHA256.HashData(Encoding.ASCII.GetBytes(verifier)));
        return CryptographicOperations.FixedTimeEquals(
            Encoding.ASCII.GetBytes(computed),
            Encoding.ASCII.GetBytes(expectedChallenge));
    }

    public static string Hash(string value) =>
        Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(value)));
}
