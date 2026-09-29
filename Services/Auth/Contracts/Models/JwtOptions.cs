namespace CompanyHQ.Services.Auth.Contracts.Models;

/// <summary>
/// Token issuing and validation settings, from the <see cref="SectionName"/> configuration section
/// (<c>Jwt__SigningKey</c> and so on in the environment).
/// </summary>
public sealed class JwtOptions
{
    public const string SectionName = "Jwt";

    /// <summary>HS256 needs at least 256 bits of key material, and refuses to sign with less.</summary>
    public const int MinimumSigningKeyLength = 32;

    public string Issuer { get; set; } = "CompanyHQ";

    public string Audience { get; set; } = "CompanyHQ";

    public string SigningKey { get; set; } = string.Empty;

    /// <summary>
    /// Short by design: an access token cannot be revoked, so its lifetime is the window an
    /// exfiltrated one stays useful. The refresh token is the revocable half.
    /// </summary>
    public int AccessTokenMinutes { get; set; } = 15;

    public int RefreshTokenDays { get; set; } = 30;

    /// <summary>Tolerance for clock drift between the API and a client's device.</summary>
    public int ClockSkewSeconds { get; set; } = 30;

    public TimeSpan AccessTokenLifetime => TimeSpan.FromMinutes(AccessTokenMinutes);

    public TimeSpan RefreshTokenLifetime => TimeSpan.FromDays(RefreshTokenDays);

    /// <summary>
    /// Called during registration so a missing or weak key fails at startup rather than on the
    /// first login attempt.
    /// </summary>
    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(SigningKey))
        {
            throw new InvalidOperationException(
                "No JWT signing key configured. Set Jwt:SigningKey.");
        }

        if (SigningKey.Length < MinimumSigningKeyLength)
        {
            throw new InvalidOperationException(
                $"The JWT signing key must be at least {MinimumSigningKeyLength} characters.");
        }

        if (AccessTokenMinutes <= 0 || RefreshTokenDays <= 0)
        {
            throw new InvalidOperationException("Token lifetimes must be positive.");
        }
    }
}
