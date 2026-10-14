using System.Security.Cryptography;
using System.Text;

using Microsoft.IdentityModel.Tokens;

using CompanyHQ.Auth.Contracts.Enums;
using CompanyHQ.Auth.Contracts.Models;
using CompanyHQ.Persistence.Services;

namespace CompanyHQ.Auth.Services;

/// <inheritdoc cref="IAuthTokenService"/>
/// <remarks>
/// Goes through <see cref="IPersistence"/> rather than <c>IRepository&lt;T&gt;</c>: issuing
/// a token also invalidates its siblings, and both writes need to land in one
/// <c>SaveChangesAsync</c> or a crash between them could leave two "open" tokens for one purpose.
/// </remarks>
public sealed class AuthTokenService(IPersistence context, AuthTokenOptions options, TimeProvider clock)
    : IAuthTokenService
{
    private const int TokenBytes = 32;

    public async Task<IssuedAuthToken> IssueAsync(
        Guid userId,
        AuthTokenPurpose purpose,
        string? pendingEmail = null,
        CancellationToken cancellationToken = default)
    {
        var now = clock.GetUtcNow();

        await ConsumeOpenTokens(userId, purpose, now, cancellationToken);

        var value = Base64UrlEncoder.Encode(RandomNumberGenerator.GetBytes(TokenBytes));
        var expiresAt = now + options.LifetimeFor(purpose);

        var token = new AuthToken
        {
            UserId = userId,
            Purpose = purpose,
            TokenHash = Hash(value),
            ExpiresAt = expiresAt,
            PendingEmail = pendingEmail
        };

        context.Add(token);
        await context.SaveChangesAsync(cancellationToken);

        return new IssuedAuthToken(token.Id, value, expiresAt);
    }

    public async Task<AuthToken?> ConsumeAsync(
        string token,
        AuthTokenPurpose purpose,
        CancellationToken cancellationToken = default)
    {
        var hash = Hash(token);
        var now = clock.GetUtcNow();

        var stored = await context.Tracked<AuthToken>()
            .FirstOrDefaultAsync(candidate => candidate.TokenHash == hash, cancellationToken);

        if (stored is null || stored.Purpose != purpose || !stored.IsActive(now))
        {
            return null;
        }

        stored.ConsumedAt = now;
        await context.SaveChangesAsync(cancellationToken);

        return stored;
    }

    public async Task<AuthToken?> FindActiveAsync(
        string token,
        AuthTokenPurpose purpose,
        CancellationToken cancellationToken = default)
    {
        var hash = Hash(token);
        var stored = await context.Query<AuthToken>()
            .FirstOrDefaultAsync(candidate => candidate.TokenHash == hash, cancellationToken);

        return stored is not null && stored.Purpose == purpose && stored.IsActive(clock.GetUtcNow()) ? stored : null;
    }

    public async Task InvalidateAsync(
        Guid userId,
        AuthTokenPurpose purpose,
        CancellationToken cancellationToken = default)
    {
        var changed = await ConsumeOpenTokens(userId, purpose, clock.GetUtcNow(), cancellationToken);

        if (changed)
        {
            await context.SaveChangesAsync(cancellationToken);
        }
    }

    /// <summary>Stamps every open token of a purpose as consumed, without saving.</summary>
    private async Task<bool> ConsumeOpenTokens(
        Guid userId,
        AuthTokenPurpose purpose,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var open = await context.Tracked<AuthToken>()
            .Where(token => token.UserId == userId && token.Purpose == purpose && token.ConsumedAt == null)
            .ToListAsync(cancellationToken);

        foreach (var token in open)
        {
            token.ConsumedAt = now;
        }

        return open.Count > 0;
    }

    private static string Hash(string value) =>
        Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(value)));
}
