using CompanyHQ.Auth.Contracts.Enums;

namespace CompanyHQ.Auth.Contracts.Dtos;

/// <summary>
/// The result of an auth attempt: a session, or the reason there is none.
/// </summary>
/// <remarks>
/// A failed login is an expected outcome, not an exception — throwing for a mistyped password
/// would make the normal path pay for the abnormal one and clutter the logs.
/// </remarks>
public readonly record struct AuthOutcome(AuthSessionDto? Session, AuthError Error)
{
    public bool Succeeded => Session is not null;

    public static AuthOutcome Success(AuthSessionDto session) => new(session, AuthError.None);

    public static AuthOutcome Failure(AuthError error) => new(null, error);
}
