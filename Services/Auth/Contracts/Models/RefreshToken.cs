using CompanyHQ.Persistence.Contracts.Models;

namespace CompanyHQ.Auth.Contracts.Models;

/// <summary>
/// One issued refresh token. The row is what makes a session revocable — deleting or revoking it
/// ends the session at the next refresh, which an access token alone can never do.
/// </summary>
public sealed class RefreshToken : BaseModel
{
    public required Guid UserId { get; set; }

    /// <summary>
    /// A SHA-256 digest of the token, never the token. A database leak then hands an attacker
    /// nothing they can present to <c>/api/auth/refresh</c>.
    /// </summary>
    public required string TokenHash { get; set; }

    public DateTimeOffset ExpiresAt { get; set; }

    /// <summary>Set on rotation and on logout; a revoked row is kept so reuse is detectable.</summary>
    public DateTimeOffset? RevokedAt { get; set; }

    /// <summary>
    /// Client-supplied label ("Pixel 8", "Web"), sanitized on write. Attacker-controlled input
    /// rendered back on a security page — trimmed, control characters and non-printables stripped,
    /// truncated to 64, and stored as <c>null</c> rather than an empty string. Never render as markup.
    /// </summary>
    public string? DeviceLabel { get; set; }

    public bool IsActive(DateTimeOffset now) => RevokedAt is null && ExpiresAt > now;
}
