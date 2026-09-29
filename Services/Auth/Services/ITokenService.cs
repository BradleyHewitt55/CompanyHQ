using CompanyHQ.Services.Auth.Contracts.Models;
using CompanyHQ.Services.Users.Contracts.Models;

namespace CompanyHQ.Services.Auth.Services;

/// <summary>
/// Mints the two token kinds. It reads no database and makes no policy decision — whether a caller
/// deserves a token is <see cref="IAuthService"/>'s business.
/// </summary>
public interface ITokenService
{
    IssuedAccessToken CreateAccessToken(User user);

    IssuedRefreshToken CreateRefreshToken();

    /// <summary>Digest of a token presented by a client, for looking up its stored row.</summary>
    string HashRefreshToken(string refreshToken);
}
