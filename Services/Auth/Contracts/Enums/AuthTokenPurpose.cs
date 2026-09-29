namespace CompanyHQ.Services.Auth.Contracts.Enums;

/// <summary>
/// What a single-use <c>AuthToken</c> is for.
/// Password reset, email verification and email change are the same mechanism three times, so they
/// share one table with this discriminator instead of three near-identical ones.
/// </summary>
public enum AuthTokenPurpose
{
    PasswordReset,
    EmailVerification,
    EmailChange
}
