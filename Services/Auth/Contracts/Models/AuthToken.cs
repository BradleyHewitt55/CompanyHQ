using CompanyHQ.Services.Auth.Contracts.Enums;
using CompanyHQ.Services.Persistence.Contracts.Models;

namespace CompanyHQ.Services.Auth.Contracts.Models;

/// <summary>
/// One single-use, mailed credential — a password reset link, an email verification link, or an
/// email change confirmation. One table for all three <see cref="AuthTokenPurpose"/> values instead
/// of three near-identical ones.
/// </summary>
public sealed class AuthToken : BaseModel
{
    public required Guid UserId { get; set; }

    public required AuthTokenPurpose Purpose { get; set; }

    /// <summary>
    /// A SHA-256 digest of the token, never the token. Same discipline as
    /// <see cref="RefreshToken.TokenHash"/>: the row is found *by* this value and a 256-bit random
    /// token has no guessable input space, so an unsalted digest is correct here.
    /// </summary>
    public required string TokenHash { get; set; }

    public DateTimeOffset ExpiresAt { get; set; }

    /// <summary>Set on use; the row survives so a repeat click reads as "already used", not a 404.</summary>
    public DateTimeOffset? ConsumedAt { get; set; }

    /// <summary>Only set for <see cref="AuthTokenPurpose.EmailChange"/> — the address being moved to.</summary>
    public string? PendingEmail { get; set; }

    public bool IsActive(DateTimeOffset now) => ConsumedAt is null && ExpiresAt > now;
}
