using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using CompanyHQ.Services.Auth.Contracts.Models;
using CompanyHQ.Services.Postgres.Services;

namespace CompanyHQ.Services.Auth.Services;

/// <summary>
/// Removes one small batch of old refresh-token rows, so session storage cannot grow without bound
/// while rotation and logout remain authoritative. Scoped and resolved per tick by
/// <see cref="RefreshTokenCleanupWorker"/>.
/// </summary>
internal sealed class RefreshTokenCleanupProcessor(
    IPostgresService<RefreshToken> tokens,
    RefreshTokenCleanupOptions options,
    TimeProvider clock,
    ILogger<RefreshTokenCleanupProcessor> logger)
{
    public async Task<int> CleanupBatchAsync(CancellationToken cancellationToken)
    {
        var now = clock.GetUtcNow();
        var revokedBefore = now - options.RevokedRetention;

        var candidates = await tokens.Query()
            .Where(token => token.ExpiresAt <= now
                || (token.RevokedAt != null && token.RevokedAt <= revokedBefore))
            .OrderBy(token => token.ExpiresAt)
            .ThenBy(token => token.Id)
            .Select(token => token.Id)
            .Take(options.BatchSize)
            .ToArrayAsync(cancellationToken);

        if (candidates.Length == 0)
        {
            return 0;
        }

        // Repeat the expiry/revocation predicate in the DELETE. If logout revoked a selected row
        // after the bounded read, a fresh revocation is retained; if refresh locked it first, the
        // row is already invalid and deletion cannot strand an active session.
        var deleted = await tokens.DeleteAsync(
            token => candidates.Contains(token.Id)
                && (token.ExpiresAt <= now
                    || (token.RevokedAt != null && token.RevokedAt <= revokedBefore)),
            cancellationToken);

        if (deleted > 0)
        {
            logger.LogInformation("Removed {Count} expired or retained-revoked refresh-token rows.", deleted);
        }

        return deleted;
    }
}
