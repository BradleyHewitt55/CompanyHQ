using System.Security.Cryptography;
using System.Text;

using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

using CompanyHQ.Auth.Contracts.Models;
using CompanyHQ.Users.Contracts.Models;
using CompanyHQ.Auth.Contracts.Identity;

namespace CompanyHQ.Auth.Services;

/// <inheritdoc cref="ITokenService"/>
public sealed class JwtTokenService(JwtOptions options, TimeProvider clock) : ITokenService
{
    private const int RefreshTokenBytes = 32;

    private readonly SigningCredentials credentials = new(
        new SymmetricSecurityKey(Encoding.UTF8.GetBytes(options.SigningKey)),
        SecurityAlgorithms.HmacSha256);

    private readonly JsonWebTokenHandler handler = new();

    public IssuedAccessToken CreateAccessToken(User user)
    {
        var now = clock.GetUtcNow();
        var expiresAt = now + options.AccessTokenLifetime;

        var descriptor = new SecurityTokenDescriptor
        {
            Issuer = options.Issuer,
            Audience = options.Audience,
            IssuedAt = now.UtcDateTime,
            NotBefore = now.UtcDateTime,
            Expires = expiresAt.UtcDateTime,
            SigningCredentials = credentials,
            Claims = new Dictionary<string, object>
            {
                [AuthClaims.Subject] = user.Id.ToString(),
                [AuthClaims.Email] = user.Email,
                [AuthClaims.Role] = user.Role.ToString(),
                // A token whose subject changed their name is not wrong, just stale; the client
                // refreshes it within AccessTokenMinutes anyway.
                [AuthClaims.FirstName] = user.FirstName ?? string.Empty,
                [AuthClaims.LastName] = user.LastName ?? string.Empty,
                [JwtRegisteredClaimNames.Jti] = Guid.CreateVersion7(now).ToString()
            }
        };

        return new IssuedAccessToken(handler.CreateToken(descriptor), expiresAt);
    }

    public IssuedRefreshToken CreateRefreshToken()
    {
        var value = Base64UrlEncoder.Encode(RandomNumberGenerator.GetBytes(RefreshTokenBytes));

        return new IssuedRefreshToken(
            value,
            HashRefreshToken(value),
            clock.GetUtcNow() + options.RefreshTokenLifetime);
    }

    /// <summary>
    /// Plain SHA-256, deliberately: the row is found *by* this digest, so it has to be
    /// deterministic and unsalted. That is safe here and not for passwords because the token is 256
    /// random bits — there is no guessable input space for the speed of SHA-256 to help an attacker
    /// search.
    /// </summary>
    public string HashRefreshToken(string refreshToken) =>
        Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(refreshToken)));
}
