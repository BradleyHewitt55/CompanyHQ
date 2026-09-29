namespace CompanyHQ.Services.Auth.Contracts.Models;

/// <summary>
/// An <see cref="AuthToken"/> as it exists for the one moment both halves are known:
/// <paramref name="Value"/> goes into the email, the digest goes to the database. Nothing keeps both.
/// </summary>
public readonly record struct IssuedAuthToken(Guid TokenId, string Value, DateTimeOffset ExpiresAt);
