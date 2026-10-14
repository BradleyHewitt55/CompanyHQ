using CompanyHQ.Auth.Contracts.Dtos;
using CompanyHQ.Auth.Contracts.Forms;
using CompanyHQ.Postgres.Contracts.Dtos;
using CompanyHQ.Postgres.Contracts.Forms;

namespace CompanyHQ.Auth.Services;

/// <summary>
/// Registration, sign-in, and the lifecycle of a session's refresh token.
/// </summary>
public interface IAuthService
{
    Task<AuthOutcome> RegisterAsync(
        RegisterRequest request,
        string remoteIp,
        CancellationToken cancellationToken = default);

    Task<AuthOutcome> LoginAsync(
        LoginRequest request,
        string remoteIp,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Exchanges a refresh token for a new session, revoking the one presented. Rotation means a
    /// stolen token is useful only until its owner next refreshes.
    /// </summary>
    Task<AuthOutcome> RefreshAsync(string refreshToken, CancellationToken cancellationToken = default);

    /// <summary>Revokes one session. Silent about unknown tokens — there is nothing to leak.</summary>
    Task LogoutAsync(string refreshToken, CancellationToken cancellationToken = default);

    /// <summary>The account as it stands now, or null when it no longer exists.</summary>
    Task<AuthUserDto?> GetUserAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Redeems an email-verification token and marks the account verified.
    /// </summary>
    Task<AuthActionOutcome> VerifyEmailAsync(string token, CancellationToken cancellationToken = default);

    /// <summary>
    /// Mails a fresh verification link for an unverified address. Enumeration-safe: a caller cannot
    /// tell an unknown address, an already-verified one, and a rate-limited one apart — all three
    /// silently do nothing here, and the controller always answers 204.
    /// </summary>
    Task ResendVerificationAsync(string email, CancellationToken cancellationToken = default);

    /// <summary>
    /// Mails a reset link for a registered address. Enumeration-safe in the same way as
    /// <see cref="ResendVerificationAsync"/> — an unknown address and a rate-limited one both do
    /// nothing here, and the controller always answers 204.
    /// </summary>
    Task ForgotPasswordAsync(string email, string remoteIp, CancellationToken cancellationToken = default);

    /// <summary>
    /// Redeems a reset token: sets the new password, revokes every session and every other open
    /// reset/email-change token, and signs the caller back in — a reset link already proved control
    /// of the mailbox, so bouncing to a login form afterward is pointless friction.
    /// </summary>
    Task<AuthOutcome> ResetPasswordAsync(
        string token,
        string newPassword,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Revokes every session but the one presented, which is rotated and returned — the caller is not
    /// signed out of the device they just changed their password on.
    /// </summary>
    Task<AuthOutcome> ChangePasswordAsync(
        Guid userId,
        string currentPassword,
        string newPassword,
        string callingRefreshToken,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Starts an email change: mails the confirmation to the new address and a notice to the old one.
    /// The account's email does not move until <see cref="ConfirmEmailChangeAsync"/> redeems the link.
    /// </summary>
    Task<AuthActionOutcome> ChangeEmailAsync(
        Guid userId,
        string currentPassword,
        string newEmail,
        CancellationToken cancellationToken = default);

    /// <summary>Moves the account's email, revokes every session, and stamps <c>SessionsRevokedAt</c>.</summary>
    Task<AuthActionOutcome> ConfirmEmailChangeAsync(string token, CancellationToken cancellationToken = default);

    /// <summary>
    /// First half of deleting the caller's own account: checks the typed name against the account
    /// and mails a single-use confirmation link. Nothing is deleted yet.
    /// </summary>
    Task<AuthActionOutcome> RequestAccountDeletionAsync(
        Guid userId,
        string fullName,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Second half: redeems the mailed link and deletes the account with everything that cascades
    /// from it. <c>Reason</c> is set when a plugged-in service's deletion guard refuses; the link
    /// then stays usable until it expires.
    /// </summary>
    Task<(AuthActionOutcome Outcome, string? Reason)> ConfirmAccountDeletionAsync(
        string token,
        CancellationToken cancellationToken = default);

    /// <summary>Active sessions only, newest first. <paramref name="currentSessionId"/> flags one as current.</summary>
    Task<PagedResult<AuthSessionSummaryDto>> GetSessionsAsync(
        Guid userId,
        Guid? currentSessionId,
        PageRequest paging,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Ends one session. Does <b>not</b> stamp <c>SessionsRevokedAt</c> — ending one device must not
    /// log out the other four. False when the row is not the caller's, for a 404.
    /// </summary>
    Task<bool> DeleteSessionAsync(Guid userId, Guid sessionId, CancellationToken cancellationToken = default);

    /// <summary>Sign out everywhere, including the caller. Stamps <c>SessionsRevokedAt</c>.</summary>
    Task RevokeAllSessionsAsync(Guid userId, CancellationToken cancellationToken = default);
}
