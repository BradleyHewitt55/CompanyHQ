using CompanyHQ.Services.Auth.Contracts.Models;

namespace CompanyHQ.Services.Auth.Services;

/// <summary>
/// Sends the credential-lifecycle emails. Every method is best-effort: a failed send is logged
/// and swallowed here, never surfaced to the caller — a mailed token is a convenience on top of an
/// account that already exists or already changed, not a precondition for either.
/// </summary>
public interface IAuthEmailSender
{
    Task SendVerificationEmailAsync(
        string email,
        IssuedAuthToken token,
        CancellationToken cancellationToken = default);

    Task SendPasswordResetEmailAsync(
        string email,
        IssuedAuthToken token,
        CancellationToken cancellationToken = default);

    Task SendEmailChangeConfirmationAsync(
        string newEmail,
        IssuedAuthToken token,
        CancellationToken cancellationToken = default);

    Task SendPasswordChangedNoticeAsync(
        Guid userId,
        string email,
        DateTimeOffset changedAt,
        CancellationToken cancellationToken = default);

    Task SendEmailChangingNoticeAsync(
        Guid userId,
        string email,
        string newEmail,
        DateTimeOffset requestedAt,
        CancellationToken cancellationToken = default);

    Task SendAccountDeletionConfirmationAsync(
        string email,
        IssuedAuthToken token,
        CancellationToken cancellationToken = default);
}
