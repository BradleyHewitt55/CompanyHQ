using CompanyHQ.Services.Auth.Contracts.Enums;

namespace CompanyHQ.Services.Auth.Contracts.Models;

/// <summary>
/// Lifetimes for each <see cref="AuthTokenPurpose"/> and the web origin every token-carrying email
/// links to.
/// </summary>
public sealed class AuthTokenOptions
{
    public const string SectionName = "Auth";

    /// <summary>A reset link is a live credential to a live account, so it gets the short fuse.</summary>
    public int PasswordResetMinutes { get; set; } = 60;

    public int EmailVerificationHours { get; set; } = 24;

    public int EmailChangeMinutes { get; set; } = 60;

    public int AccountDeletionMinutes { get; set; } = 60;

    /// <summary>
    /// Origin every token-carrying email links to, e.g. "https://app.companyhq.example" — never a
    /// mobile deep link, so the link works in whatever mail client the person is standing in.
    /// </summary>
    public string WebBaseAddress { get; set; } = string.Empty;

    public TimeSpan LifetimeFor(AuthTokenPurpose purpose) => purpose switch
    {
        AuthTokenPurpose.PasswordReset => TimeSpan.FromMinutes(PasswordResetMinutes),
        AuthTokenPurpose.EmailVerification => TimeSpan.FromHours(EmailVerificationHours),
        AuthTokenPurpose.EmailChange => TimeSpan.FromMinutes(EmailChangeMinutes),
        AuthTokenPurpose.AccountDeletion => TimeSpan.FromMinutes(AccountDeletionMinutes),
        _ => throw new ArgumentOutOfRangeException(nameof(purpose), purpose, "Unknown auth token purpose.")
    };

    /// <summary>
    /// Called during registration so a missing web address fails at startup rather than producing an
    /// email with <c>href="/auth/reset-password?token=…"</c>.
    /// </summary>
    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(WebBaseAddress))
        {
            throw new InvalidOperationException(
                "No web base address configured. Set Auth:WebBaseAddress.");
        }

        if (!Uri.TryCreate(WebBaseAddress, UriKind.Absolute, out _))
        {
            throw new InvalidOperationException("Auth:WebBaseAddress must be an absolute URL.");
        }

        if (PasswordResetMinutes <= 0 || EmailVerificationHours <= 0 || EmailChangeMinutes <= 0
            || AccountDeletionMinutes <= 0)
        {
            throw new InvalidOperationException("Auth token lifetimes must be positive.");
        }
    }
}
