using System.Text.Json.Serialization;

namespace CompanyHQ.Services.Auth.Contracts.Enums;

/// <summary>
/// Why an auth attempt failed, in the terms the controller maps to a status code.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter<AuthError>))]
public enum AuthError
{
    None,

    /// <summary>
    /// A wrong password and an unknown email produce this same value — telling them apart turns
    /// the login endpoint into an account-enumeration oracle.
    /// </summary>
    InvalidCredentials,

    /// <summary>Too many attempts for the observed IP or normalized login identity.</summary>
    RateLimited,

    EmailTaken,

    /// <summary>Unknown, expired, or already-rotated refresh token.</summary>
    InvalidSession,

    /// <summary>An <c>AuthToken</c> that is unknown, expired, or already consumed.</summary>
    InvalidToken,

    /// <summary>A new password equal to the current one — the one check beyond length.</summary>
    WeakPassword,

    /// <summary>The name typed to confirm an account deletion is not the account's own.</summary>
    NameMismatch
}
