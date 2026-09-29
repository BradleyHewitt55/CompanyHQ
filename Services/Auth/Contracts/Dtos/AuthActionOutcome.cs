using CompanyHQ.Services.Auth.Contracts.Enums;

namespace CompanyHQ.Services.Auth.Contracts.Dtos;

/// <summary>
/// The result of an auth action that returns no session — verify-email, confirm-email-change,
/// session revocation. Same reasoning as <see cref="AuthOutcome"/>: an expected failure is a value,
/// not an exception.
/// </summary>
public readonly record struct AuthActionOutcome(bool Succeeded, AuthError Error)
{
    public static AuthActionOutcome Success() => new(true, AuthError.None);

    public static AuthActionOutcome Failure(AuthError error) => new(false, error);
}
