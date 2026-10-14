using System.Data;
using System.Text;

using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;

using CompanyHQ.Auth.Contracts.Dtos;
using CompanyHQ.Auth.Contracts.Enums;
using CompanyHQ.Auth.Contracts.Forms;
using CompanyHQ.Auth.Contracts.Models;
using CompanyHQ.Postgres.Contracts;
using CompanyHQ.Postgres.Contracts.Dtos;
using CompanyHQ.Postgres.Contracts.Forms;
using CompanyHQ.Persistence.Services;
using CompanyHQ.Users.Contracts.Enums;
using CompanyHQ.Users.Contracts.Models;
using CompanyHQ.Users.Services;

namespace CompanyHQ.Auth.Services;

/// <inheritdoc cref="IAuthService"/>
/// <remarks>
/// Goes through <see cref="IPersistence"/> rather than <c>IRepository&lt;T&gt;</c> because
/// rotation revokes one row and inserts another: two repository calls would be two transactions,
/// and a crash between them would strand the session.
/// </remarks>
public sealed class AuthService(
    IPersistence context,
    ITokenService tokens,
    IAuthTokenService authTokens,
    IAuthEmailSender authEmail,
    IPasswordHasher<User> passwordHasher,
    IAuthRateLimiter rateLimiter,
    IAccountDeletionCoordinator accountDeletion,
    LoginRateLimitOptions loginRateLimitOptions,
    TimeProvider clock,
    ILogger<AuthService> logger) : IAuthService
{
    public async Task<AuthOutcome> RegisterAsync(
        RegisterRequest request,
        string remoteIp,
        CancellationToken cancellationToken = default)
    {
        var email = Normalise(request.Email);
        var ipDecision = await rateLimiter.CheckAsync(
            "register",
            $"ip:{remoteIp}",
            AuthRateLimits.RegistrationIpMaxAttempts,
            AuthRateLimits.RegistrationWindow,
            cancellationToken);
        var identityDecision = await rateLimiter.CheckAsync(
            "register",
            $"identity:{email}",
            AuthRateLimits.RegistrationIdentityMaxAttempts,
            AuthRateLimits.RegistrationWindow,
            cancellationToken);

        if (!ipDecision.Allowed || !identityDecision.Allowed)
        {
            return AuthOutcome.Failure(AuthError.RateLimited);
        }

        Func<Task<(AuthOutcome Outcome, IssuedAuthToken Verification)>> work = async () =>
        {
            if (await context.Tracked<User>().AnyAsync(user => user.Email == email, cancellationToken))
            {
                return (AuthOutcome.Failure(AuthError.EmailTaken), default);
            }

            var user = await CreateUserAsync(
                email,
                request.Password,
                request.FirstName,
                request.LastName,
                request.Bio,
                cancellationToken);

            var session = await IssueSession(user, cancellationToken);
            var verification = await authTokens.IssueAsync(
                user.Id,
                AuthTokenPurpose.EmailVerification,
                cancellationToken: cancellationToken);
            return (AuthOutcome.Success(session), verification);
        };

        try
        {
            var (outcome, verification) = await context.InTransactionAsync(
                _ => work(),
                IsolationLevel.Serializable,
                cancellationToken);

            if (outcome.Session is { } session)
            {
                // Best-effort and after the commit: a send failure must never turn a created account
                // into a failed registration. resend-verification is the recovery.
                await authEmail.SendVerificationEmailAsync(session.User.Email, verification, cancellationToken);
            }

            return outcome;
        }
        catch (UniqueConstraintException exception) when (exception.Is("ix_user_email"))
        {
            return AuthOutcome.Failure(AuthError.EmailTaken);
        }
    }

    public async Task<AuthActionOutcome> VerifyEmailAsync(
        string token,
        CancellationToken cancellationToken = default)
    {
        var consumed = await authTokens.ConsumeAsync(token, AuthTokenPurpose.EmailVerification, cancellationToken);
        if (consumed is null)
        {
            return AuthActionOutcome.Failure(AuthError.InvalidToken);
        }

        var user = await context.Tracked<User>()
            .FirstOrDefaultAsync(candidate => candidate.Id == consumed.UserId, cancellationToken);
        if (user is null)
        {
            return AuthActionOutcome.Failure(AuthError.InvalidToken);
        }

        user.EmailVerified = true;
        await context.SaveChangesAsync(cancellationToken);

        return AuthActionOutcome.Success();
    }

    public async Task ResendVerificationAsync(string email, CancellationToken cancellationToken = default)
    {
        var normalised = Normalise(email);
        var decision = await rateLimiter.CheckAsync(
            "resend-verification",
            $"identity:{normalised}",
            AuthRateLimits.ResendVerificationMaxAttempts,
            AuthRateLimits.ResendVerificationWindow,
            cancellationToken);

        if (!decision.Allowed)
        {
            return;
        }

        var user = await context.Tracked<User>()
            .FirstOrDefaultAsync(
                candidate => candidate.Email == normalised && !candidate.EmailVerified,
                cancellationToken);

        // Unknown address or already verified: same silent no-op, so this endpoint cannot be used to
        // learn which addresses exist or which ones still need confirming.
        if (user is null)
        {
            return;
        }

        var token = await authTokens.IssueAsync(
            user.Id,
            AuthTokenPurpose.EmailVerification,
            cancellationToken: cancellationToken);

        await authEmail.SendVerificationEmailAsync(user.Email, token, cancellationToken);
    }

    public async Task ForgotPasswordAsync(
        string email,
        string remoteIp,
        CancellationToken cancellationToken = default)
    {
        var normalised = Normalise(email);

        var identityDecision = await rateLimiter.CheckAsync(
            "forgot-password",
            $"identity:{normalised}",
            AuthRateLimits.ForgotPasswordIdentityMaxAttempts,
            AuthRateLimits.ForgotPasswordWindow,
            cancellationToken);
        var ipDecision = await rateLimiter.CheckAsync(
            "forgot-password",
            $"ip:{remoteIp}",
            AuthRateLimits.ForgotPasswordIpMaxAttempts,
            AuthRateLimits.ForgotPasswordWindow,
            cancellationToken);

        // Rate-limited is still a silent no-op: a 429 here would tell an attacker their guessed
        // address is real enough to be worth limiting.
        if (!identityDecision.Allowed || !ipDecision.Allowed)
        {
            return;
        }

        var user = await context.Tracked<User>()
            .FirstOrDefaultAsync(candidate => candidate.Email == normalised, cancellationToken);

        if (user is null)
        {
            return;
        }

        var token = await authTokens.IssueAsync(
            user.Id,
            AuthTokenPurpose.PasswordReset,
            cancellationToken: cancellationToken);

        await authEmail.SendPasswordResetEmailAsync(user.Email, token, cancellationToken);
    }

    public async Task<AuthOutcome> ResetPasswordAsync(
        string token,
        string newPassword,
        CancellationToken cancellationToken = default)
    {
        Func<Task<(AuthOutcome Outcome, DateTimeOffset ChangedAt)>> work = async () =>
        {
            var consumed = await authTokens.ConsumeAsync(token, AuthTokenPurpose.PasswordReset, cancellationToken);
            if (consumed is null)
            {
                return (AuthOutcome.Failure(AuthError.InvalidToken), default);
            }

            var user = await context.Tracked<User>()
                .FirstOrDefaultAsync(candidate => candidate.Id == consumed.UserId, cancellationToken);
            if (user is null)
            {
                return (AuthOutcome.Failure(AuthError.InvalidToken), default);
            }

            var now = clock.GetUtcNow();

            // The whole point of a reset is usually "someone else is in my account" — every session
            // and every other outstanding reset/email-change credential dies with it, no exceptions.
            user.PasswordHash = passwordHasher.HashPassword(user, newPassword);
            user.SessionsRevokedAt = now;

            var activeSessions = await context.Tracked<RefreshToken>()
                .Where(candidate => candidate.UserId == user.Id && candidate.RevokedAt == null)
                .ToListAsync(cancellationToken);
            foreach (var active in activeSessions)
            {
                active.RevokedAt = now;
            }

            await context.SaveChangesAsync(cancellationToken);
            await authTokens.InvalidateAsync(user.Id, AuthTokenPurpose.PasswordReset, cancellationToken);
            await authTokens.InvalidateAsync(user.Id, AuthTokenPurpose.EmailChange, cancellationToken);

            // Proved control of the mailbox already; bouncing to a login form now is pointless friction.
            var session = await IssueSession(user, cancellationToken);
            return (AuthOutcome.Success(session), now);
        };

        var (outcome, changedAt) = await context.InTransactionAsync(
            _ => work(),
            IsolationLevel.ReadCommitted,
            cancellationToken);

        if (outcome.Session is { } session)
        {
            // Best-effort and after the commit: a notice failing to send must never undo the reset.
            await authEmail.SendPasswordChangedNoticeAsync(
                session.User.Id,
                session.User.Email,
                changedAt,
                cancellationToken);
        }

        return outcome;
    }

    public async Task<AuthOutcome> ChangePasswordAsync(
        Guid userId,
        string currentPassword,
        string newPassword,
        string callingRefreshToken,
        CancellationToken cancellationToken = default)
    {
        var decision = await rateLimiter.CheckAsync(
            "change-password",
            $"user:{userId}",
            AuthRateLimits.ChangeCredentialMaxAttempts,
            AuthRateLimits.ChangeCredentialWindow,
            cancellationToken);

        if (!decision.Allowed)
        {
            return AuthOutcome.Failure(AuthError.RateLimited);
        }

        Func<Task<(AuthOutcome Outcome, DateTimeOffset ChangedAt)>> work = async () =>
        {
            var user = await context.Tracked<User>()
                .FirstOrDefaultAsync(candidate => candidate.Id == userId, cancellationToken);
            if (user is null ||
                passwordHasher.VerifyHashedPassword(user, user.PasswordHash, currentPassword)
                    is PasswordVerificationResult.Failed)
            {
                return (AuthOutcome.Failure(AuthError.InvalidCredentials), default);
            }

            if (string.Equals(newPassword, currentPassword, StringComparison.Ordinal))
            {
                return (AuthOutcome.Failure(AuthError.WeakPassword), default);
            }

            var callingHash = tokens.HashRefreshToken(callingRefreshToken);
            var sessions = await context.Tracked<RefreshToken>()
                .Where(candidate => candidate.UserId == user.Id && candidate.RevokedAt == null)
                .ToListAsync(cancellationToken);
            var calling = sessions.FirstOrDefault(candidate => candidate.TokenHash == callingHash);

            if (calling is null)
            {
                return (AuthOutcome.Failure(AuthError.InvalidSession), default);
            }

            var now = clock.GetUtcNow();
            user.PasswordHash = passwordHasher.HashPassword(user, newPassword);
            user.SessionsRevokedAt = now;

            // Every other session dies; the caller's own is rotated below rather than left alive
            // unrotated, so the device that made this change is not carrying a now-stale token either.
            foreach (var session in sessions)
            {
                if (session != calling)
                {
                    session.RevokedAt = now;
                }
            }

            var deviceLabel = calling.DeviceLabel;
            calling.RevokedAt = now;
            await context.SaveChangesAsync(cancellationToken);

            var issued = await IssueSession(user, cancellationToken, deviceLabel: deviceLabel);
            return (AuthOutcome.Success(issued), now);
        };

        var (outcome, changedAt) = await context.InTransactionAsync(
            _ => work(),
            IsolationLevel.ReadCommitted,
            cancellationToken);

        if (outcome.Session is { } session)
        {
            await authEmail.SendPasswordChangedNoticeAsync(
                session.User.Id,
                session.User.Email,
                changedAt,
                cancellationToken);
        }

        return outcome;
    }

    public async Task<AuthActionOutcome> ChangeEmailAsync(
        Guid userId,
        string currentPassword,
        string newEmail,
        CancellationToken cancellationToken = default)
    {
        var decision = await rateLimiter.CheckAsync(
            "change-email",
            $"user:{userId}",
            AuthRateLimits.ChangeCredentialMaxAttempts,
            AuthRateLimits.ChangeCredentialWindow,
            cancellationToken);

        if (!decision.Allowed)
        {
            return AuthActionOutcome.Failure(AuthError.RateLimited);
        }

        var normalisedNewEmail = Normalise(newEmail);

        var user = await context.Tracked<User>()
            .FirstOrDefaultAsync(candidate => candidate.Id == userId, cancellationToken);
        if (user is null ||
            passwordHasher.VerifyHashedPassword(user, user.PasswordHash, currentPassword)
                is PasswordVerificationResult.Failed)
        {
            return AuthActionOutcome.Failure(AuthError.InvalidCredentials);
        }

        if (await context.Tracked<User>()
            .AnyAsync(candidate => candidate.Email == normalisedNewEmail, cancellationToken))
        {
            return AuthActionOutcome.Failure(AuthError.EmailTaken);
        }

        var token = await authTokens.IssueAsync(
            user.Id,
            AuthTokenPurpose.EmailChange,
            pendingEmail: normalisedNewEmail,
            cancellationToken: cancellationToken);

        var requestedAt = clock.GetUtcNow();
        await authEmail.SendEmailChangeConfirmationAsync(normalisedNewEmail, token, cancellationToken);
        await authEmail.SendEmailChangingNoticeAsync(
            user.Id,
            user.Email,
            normalisedNewEmail,
            requestedAt,
            cancellationToken);

        return AuthActionOutcome.Success();
    }

    public async Task<AuthActionOutcome> RequestAccountDeletionAsync(
        Guid userId,
        string fullName,
        CancellationToken cancellationToken = default)
    {
        var user = await context.Query<User>()
            .Where(candidate => candidate.Id == userId)
            .Select(candidate => new { candidate.Email, candidate.FirstName, candidate.LastName })
            .FirstOrDefaultAsync(cancellationToken);
        if (user is null)
        {
            return AuthActionOutcome.Failure(AuthError.InvalidSession);
        }

        // An account with no name on it confirms with its address instead.
        var expected = $"{user.FirstName} {user.LastName}".Trim();
        if (expected.Length == 0)
        {
            expected = user.Email;
        }

        if (!string.Equals(string.Join(' ', fullName.Split(' ', StringSplitOptions.RemoveEmptyEntries)),
                expected, StringComparison.OrdinalIgnoreCase))
        {
            return AuthActionOutcome.Failure(AuthError.NameMismatch);
        }

        var token = await authTokens.IssueAsync(userId, AuthTokenPurpose.AccountDeletion, cancellationToken: cancellationToken);
        await authEmail.SendAccountDeletionConfirmationAsync(user.Email, token, cancellationToken);
        return AuthActionOutcome.Success();
    }

    public async Task<(AuthActionOutcome Outcome, string? Reason)> ConfirmAccountDeletionAsync(
        string token,
        CancellationToken cancellationToken = default)
    {
        var stored = await authTokens.FindActiveAsync(token, AuthTokenPurpose.AccountDeletion, cancellationToken);
        if (stored is null)
        {
            return (AuthActionOutcome.Failure(AuthError.InvalidToken), null);
        }

        // Sessions, tokens and every row with a cascading key to the account go with it.
        var (result, reason) = await accountDeletion.DeleteAsync(stored.UserId, cancellationToken);
        return result switch
        {
            AccountDeletionOutcome.Deleted => (AuthActionOutcome.Success(), null),
            AccountDeletionOutcome.Blocked => (AuthActionOutcome.Failure(AuthError.None), reason),
            _ => (AuthActionOutcome.Failure(AuthError.InvalidToken), null)
        };
    }

    public async Task<AuthActionOutcome> ConfirmEmailChangeAsync(
        string token,
        CancellationToken cancellationToken = default)
    {
        Func<Task<AuthActionOutcome>> work = async () =>
        {
            var consumed = await authTokens.ConsumeAsync(token, AuthTokenPurpose.EmailChange, cancellationToken);
            if (consumed is null || string.IsNullOrWhiteSpace(consumed.PendingEmail))
            {
                return AuthActionOutcome.Failure(AuthError.InvalidToken);
            }

            var user = await context.Tracked<User>()
                .FirstOrDefaultAsync(candidate => candidate.Id == consumed.UserId, cancellationToken);
            if (user is null)
            {
                return AuthActionOutcome.Failure(AuthError.InvalidToken);
            }

            var now = clock.GetUtcNow();

            // The confirmation link was clicked at the new address, which is itself proof of control
            // — the same fact email verification exists to establish.
            user.Email = consumed.PendingEmail;
            user.EmailVerified = true;
            user.SessionsRevokedAt = now;

            var activeSessions = await context.Tracked<RefreshToken>()
                .Where(candidate => candidate.UserId == user.Id && candidate.RevokedAt == null)
                .ToListAsync(cancellationToken);
            foreach (var active in activeSessions)
            {
                active.RevokedAt = now;
            }

            await context.SaveChangesAsync(cancellationToken);
            return AuthActionOutcome.Success();
        };

        try
        {
            return await context.InTransactionAsync(
                _ => work(),
                IsolationLevel.ReadCommitted,
                cancellationToken);
        }
        catch (UniqueConstraintException exception) when (exception.Is("ix_user_email"))
        {
            // Two confirmations racing for the same address: the second loses, cleanly.
            return AuthActionOutcome.Failure(AuthError.EmailTaken);
        }
    }

    public async Task<AuthOutcome> LoginAsync(
        LoginRequest request,
        string remoteIp,
        CancellationToken cancellationToken = default)
    {
        var email = Normalise(request.Email);

        // IP and normalized identity are independent dimensions: a distributed guesser rotating IPs
        // still trips the identity counter, and a shared IP (NAT, campus wifi) trips only its own.
        var ipDecision = await rateLimiter.CheckAsync(
            "login", $"ip:{remoteIp}", loginRateLimitOptions.MaxAttempts, loginRateLimitOptions.Window, cancellationToken);
        var identityDecision = await rateLimiter.CheckAsync(
            "login", $"identity:{email}", loginRateLimitOptions.MaxAttempts, loginRateLimitOptions.Window, cancellationToken);

        if (!ipDecision.Allowed || !identityDecision.Allowed)
        {
            return AuthOutcome.Failure(AuthError.RateLimited);
        }

        var user = await context.Tracked<User>()
            .FirstOrDefaultAsync(candidate => candidate.Email == email, cancellationToken);
        var verificationUser = user ?? DummyUser;
        var passwordHash = user?.PasswordHash ?? DummyPasswordHash;
        var verification = passwordHasher.VerifyHashedPassword(
            verificationUser,
            passwordHash,
            request.Password);

        if (user is null || verification is PasswordVerificationResult.Failed)
        {
            return AuthOutcome.Failure(AuthError.InvalidCredentials);
        }

        // The hasher asks for this when its iteration count has moved on since the hash was written.
        if (verification is PasswordVerificationResult.SuccessRehashNeeded)
        {
            user.PasswordHash = passwordHasher.HashPassword(user, request.Password);
        }

        var session = await IssueSession(user, cancellationToken, deviceLabel: SanitizeDeviceLabel(request.DeviceLabel));
        return AuthOutcome.Success(session);
    }

    public Task<AuthOutcome> RefreshAsync(
        string refreshToken,
        CancellationToken cancellationToken = default)
    {
        var hash = tokens.HashRefreshToken(refreshToken);

        Func<Task<AuthOutcome>> work = async () =>
        {
            // FOR UPDATE is the single-winner gate. A simultaneous request waits for this row,
            // then re-reads the committed revoked value instead of issuing a second session.
            var stored = await context.Locked<RefreshToken>()
                .FirstOrDefaultAsync(candidate => candidate.TokenHash == hash, cancellationToken);
            var now = clock.GetUtcNow();

            // A rotated token being replayed means either a stolen token or a client that raced
            // itself. Both are correctly answered by "everyone signs in again" — the family-tree
            // version of this (chains, generations) is a lot of machinery for a nicer log line.
            //
            // Excluded: a token that died as part of a deliberate mass revocation (reset-password,
            // change-password, confirm-email-change, revoke-all). Every one of those already gave the
            // account its "everyone signs in again" moment; without this carve-out, a second device's
            // routine next refresh with its now-stale token would cascade and kill the session the
            // person is actively using right after they reset their own password.
            var ownerSessionsRevokedAt = stored is null
                ? null
                : await context.Query<User>()
                    .Where(candidate => candidate.Id == stored.UserId)
                    .Select(candidate => candidate.SessionsRevokedAt)
                    .FirstOrDefaultAsync(cancellationToken);
            var diedInMassRevocation = stored?.RevokedAt is { } revokedAt &&
                ownerSessionsRevokedAt is { } sessionsRevokedAt &&
                revokedAt <= sessionsRevokedAt;

            if (stored is not null && stored.RevokedAt is not null && !diedInMassRevocation)
            {
                var active = await context.Tracked<RefreshToken>()
                    .Where(candidate => candidate.UserId == stored.UserId && candidate.RevokedAt == null)
                    .ToListAsync(cancellationToken);
                foreach (var activeSession in active)
                {
                    activeSession.RevokedAt = now;
                }

                await context.SaveChangesAsync(cancellationToken);
                logger.LogWarning(
                    "Refresh token reuse detected for user {UserId}, token {TokenId}.",
                    stored.UserId,
                    stored.Id);
                return AuthOutcome.Failure(AuthError.InvalidSession);
            }

            if (stored is null || !stored.IsActive(now))
            {
                return AuthOutcome.Failure(AuthError.InvalidSession);
            }

            var user = await context.Tracked<User>()
                .FirstOrDefaultAsync(candidate => candidate.Id == stored.UserId, cancellationToken);

            if (user is null)
            {
                return AuthOutcome.Failure(AuthError.InvalidSession);
            }

            stored.RevokedAt = now;
            var session = await IssueSession(user, cancellationToken, deviceLabel: stored.DeviceLabel);
            return AuthOutcome.Success(session);
        };

        return context.InTransactionAsync(
            _ => work(),
            IsolationLevel.ReadCommitted,
            cancellationToken);
    }

    public async Task LogoutAsync(string refreshToken, CancellationToken cancellationToken = default)
    {
        var hash = tokens.HashRefreshToken(refreshToken);

        var stored = await context.Tracked<RefreshToken>()
            .FirstOrDefaultAsync(token => token.TokenHash == hash, cancellationToken);

        if (stored is null || stored.RevokedAt is not null)
        {
            return;
        }

        stored.RevokedAt = clock.GetUtcNow();

        await context.SaveChangesAsync(cancellationToken);
    }

    public async Task<PagedResult<AuthSessionSummaryDto>> GetSessionsAsync(
        Guid userId,
        Guid? currentSessionId,
        PageRequest paging,
        CancellationToken cancellationToken = default)
    {
        var now = clock.GetUtcNow();

        var page = await context.Query<RefreshToken>()
            .Where(candidate => candidate.UserId == userId && candidate.RevokedAt == null && candidate.ExpiresAt > now)
            .OrderByDescending(candidate => candidate.CreatedAt).ThenBy(candidate => candidate.Id)
            .ToPageAsync(
                paging.Skip,
                paging.PageSize,
                candidate => new AuthSessionSummaryDto(
                    candidate.Id,
                    candidate.DeviceLabel,
                    candidate.CreatedAt,
                    candidate.ExpiresAt,
                    candidate.Id == currentSessionId),
                cancellationToken);

        return new PagedResult<AuthSessionSummaryDto>(page.Items, paging.Page, paging.PageSize, page.Total);
    }

    public async Task<bool> DeleteSessionAsync(
        Guid userId,
        Guid sessionId,
        CancellationToken cancellationToken = default)
    {
        var session = await context.Tracked<RefreshToken>()
            .FirstOrDefaultAsync(
                candidate => candidate.Id == sessionId && candidate.UserId == userId,
                cancellationToken);

        // Never 404-versus-403 distinguishes "not yours" from "already gone" — both read the same to
        // a caller who should not learn whether a session id they don't own exists at all.
        if (session is null || session.RevokedAt is not null)
        {
            return false;
        }

        session.RevokedAt = clock.GetUtcNow();
        await context.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task RevokeAllSessionsAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var now = clock.GetUtcNow();

        var user = await context.Tracked<User>().FirstOrDefaultAsync(candidate => candidate.Id == userId, cancellationToken);
        if (user is null)
        {
            return;
        }

        // Unlike DeleteSessionAsync, this is the account holder deliberately signing out everywhere —
        // including whichever access token is making this very call, hence the stamp.
        user.SessionsRevokedAt = now;

        var sessions = await context.Tracked<RefreshToken>()
            .Where(candidate => candidate.UserId == userId && candidate.RevokedAt == null)
            .ToListAsync(cancellationToken);
        foreach (var session in sessions)
        {
            session.RevokedAt = now;
        }

        await context.SaveChangesAsync(cancellationToken);
    }

    public async Task<AuthUserDto?> GetUserAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var user = await context.Query<User>()
            .Where(user => user.Id == id)
            .Select(user => new
            {
                user.Id,
                user.Email,
                user.FirstName,
                user.LastName,
                user.Role,
                user.EmailVerified,
                RoleName = user.AccessRole == null ? null : user.AccessRole.Name,
                Permissions = user.AccessRole == null ? null : user.AccessRole.Permissions
            })
            .FirstOrDefaultAsync(cancellationToken);

        // An account with no role row yet reads as its legacy role, exactly as token validation does.
        return user is null
            ? null
            : new AuthUserDto(
                user.Id,
                user.Email,
                user.FirstName,
                user.LastName,
                user.Role,
                user.EmailVerified,
                user.RoleName ?? user.Role.ToString(),
                user.Permissions ?? (user.Role is UserRole.Admin ? AccessPermissions.All : AccessPermissions.Client));
    }

    /// <summary>
    /// Stores the new refresh token and commits it together with whatever the caller already
    /// staged — the rehashed password or the revoked predecessor.
    /// </summary>
    private async Task<AuthSessionDto> IssueSession(
        User user,
        CancellationToken cancellationToken,
        string? deviceLabel = null)
    {
        var access = tokens.CreateAccessToken(user);
        var refresh = tokens.CreateRefreshToken();

        var row = new RefreshToken
        {
            UserId = user.Id,
            TokenHash = refresh.Hash,
            ExpiresAt = refresh.ExpiresAt,
            DeviceLabel = deviceLabel
        };
        context.Add(row);

        await context.SaveChangesAsync(cancellationToken);

        return new AuthSessionDto(
            access.Value,
            access.ExpiresAt,
            refresh.Value,
            refresh.ExpiresAt,
            row.Id,
            await GetUserAsync(user.Id, cancellationToken)
                ?? throw new InvalidOperationException("The account disappeared while its session was issued."));
    }

    /// <summary>
    /// Attacker-controlled input rendered back on a security page: trim, strip control characters,
    /// truncate to 64 <em>after</em> stripping (never silently truncate first and accept whatever was
    /// cut into), and store <c>null</c> rather than an empty string.
    /// </summary>
    private static string? SanitizeDeviceLabel(string? label)
    {
        if (string.IsNullOrWhiteSpace(label))
        {
            return null;
        }

        var trimmed = label.Trim();
        var printable = new StringBuilder(trimmed.Length);
        foreach (var character in trimmed)
        {
            if (!char.IsControl(character))
            {
                printable.Append(character);
            }
        }

        var cleaned = printable.ToString().Trim();
        if (cleaned.Length > 64)
        {
            cleaned = cleaned[..64];
        }

        return cleaned.Length == 0 ? null : cleaned;
    }

    private static readonly User DummyUser = new()
    {
        Email = "unknown-login@invalid.companyhq",
        PasswordHash = string.Empty
    };

    private static readonly string DummyPasswordHash = new PasswordHasher<User>()
        .HashPassword(DummyUser, "CompanyHQ-invalid-login-password");

    /// <summary>
    /// The only place a <see cref="User"/> row is built and its password hashed. Registration and
    /// any future sign-up path must land here so the two can never drift apart.
    /// </summary>
    private async Task<User> CreateUserAsync(
        string normalisedEmail,
        string password,
        string? firstName,
        string? lastName,
        string? bio,
        CancellationToken cancellationToken)
    {
        var user = new User
        {
            Email = normalisedEmail,
            PasswordHash = string.Empty,
            FirstName = firstName?.Trim(),
            LastName = lastName?.Trim(),
            Bio = bio?.Trim(),
            Role = UserRole.Client,
            AccessRoleId = await context.Tracked<AccessRole>().Where(role => role.Name == "Client")
                .Select(role => role.Id).SingleAsync(cancellationToken)
        };

        // Hashed against the entity because IPasswordHasher<T> takes the user as context.
        user.PasswordHash = passwordHasher.HashPassword(user, password);
        context.Add(user);
        await context.SaveChangesAsync(cancellationToken);
        return user;
    }

    /// <summary>
    /// A mailed starter password. The alphabet drops the characters people mis-copy out of an email
    /// (0/O, 1/l/I); 16 of these still clears the 12-character policy floor with room to spare.
    /// </summary>
    private static string Normalise(string email) => email.Trim().ToLowerInvariant();
}
