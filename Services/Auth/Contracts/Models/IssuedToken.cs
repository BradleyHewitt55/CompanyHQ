namespace CompanyHQ.Auth.Contracts.Models;

/// <summary>A signed bearer token and the instant it stops being accepted.</summary>
public readonly record struct IssuedAccessToken(string Value, DateTimeOffset ExpiresAt);

/// <summary>
/// A refresh token as it exists for the one moment both halves are known: <paramref name="Value"/>
/// goes to the client and <paramref name="Hash"/> goes to the database. Nothing keeps both.
/// </summary>
public readonly record struct IssuedRefreshToken(string Value, string Hash, DateTimeOffset ExpiresAt);
