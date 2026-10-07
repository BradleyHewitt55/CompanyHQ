namespace CompanyHQ.Services.Auth.Contracts.Enums;

/// <summary>
/// What a single-use <c>AuthToken</c> is for.
/// Password reset, email verification, email change and account deletion are the same mechanism,
/// so they share one table with this discriminator instead of near-identical ones.
/// </summary>
public enum AuthTokenPurpose
{
    PasswordReset,
    EmailVerification,
    EmailChange,
    AccountDeletion
}
