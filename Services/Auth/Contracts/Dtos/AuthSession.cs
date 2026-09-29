namespace CompanyHQ.Services.Auth.Contracts.Dtos;

/// <summary>
/// Everything a client needs to hold a session: the bearer token, the token that renews it, and
/// the account both belong to.
/// </summary>
/// <remarks>
/// Both expiries are absolute UTC instants rather than durations, so a client that stored the
/// session hours ago can tell whether it is still usable without having recorded when it arrived.
/// <paramref name="SessionId"/> is never a secret itself — a client passes it back to
/// <c>GET /api/auth/sessions?currentSessionId=</c> to have that listing flag which row is this one,
/// without ever sending the refresh token itself for that purpose.
/// </remarks>
public sealed record AuthSessionDto(
    string AccessToken,
    DateTimeOffset AccessTokenExpiresAt,
    string RefreshToken,
    DateTimeOffset RefreshTokenExpiresAt,
    Guid SessionId,
    AuthUserDto User);
