using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

using CompanyHQ.Services.Auth.Contracts.Dtos;
using CompanyHQ.Services.Auth.Contracts.Enums;
using CompanyHQ.Services.Auth.Contracts.Forms;
using CompanyHQ.Services.Auth.Services;
using CompanyHQ.Services.Postgres.Contracts.Dtos;
using CompanyHQ.Services.Postgres.Contracts.Forms;
using CompanyHQ.Services.Auth.Contracts.Identity;

namespace CompanyHQ.Services.Auth.Controllers;

/// <summary>
/// Sign-up, sign-in, session renewal, and the caller's own account.
/// </summary>
[ApiController]
[Route("api/auth")]
[Produces("application/json")]
public sealed class AuthController(IAuthService auth) : ControllerBase
{
    [HttpPost("register")]
    [AllowAnonymous]
    [ProducesResponseType<AuthSessionDto>(StatusCodes.Status201Created)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status429TooManyRequests)]
    public async Task<ActionResult<AuthSessionDto>> Register(
        RegisterRequest request,
        CancellationToken cancellationToken = default)
    {
        var remoteIp = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        var outcome = await auth.RegisterAsync(request, remoteIp, cancellationToken);

        return outcome.Succeeded
            ? Created($"/api/users/{outcome.Session!.User.Id}", outcome.Session)
            : Failed(outcome.Error);
    }

    /// <summary>Signing in issues a fresh session; existing ones on other devices keep working.</summary>
    [HttpPost("login")]
    [AllowAnonymous]
    [ProducesResponseType<AuthSessionDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status429TooManyRequests)]
    public async Task<ActionResult<AuthSessionDto>> Login(
        LoginRequest request,
        CancellationToken cancellationToken = default)
    {
        var remoteIp = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        var outcome = await auth.LoginAsync(request, remoteIp, cancellationToken);

        return outcome.Succeeded ? outcome.Session! : Failed(outcome.Error);
    }

    /// <summary>
    /// Anonymous by design: the expired access token is exactly what a client cannot present here.
    /// The refresh token in the body is the credential.
    /// </summary>
    [HttpPost("refresh")]
    [AllowAnonymous]
    [ProducesResponseType<AuthSessionDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<AuthSessionDto>> Refresh(
        RefreshRequest request,
        CancellationToken cancellationToken = default)
    {
        var outcome = await auth.RefreshAsync(request.RefreshToken, cancellationToken);

        return outcome.Succeeded ? outcome.Session! : Failed(outcome.Error);
    }

    /// <summary>Always 204 — whether the token existed is not the caller's business.</summary>
    [HttpPost("logout")]
    [AllowAnonymous]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Logout(
        RefreshRequest request,
        CancellationToken cancellationToken = default)
    {
        await auth.LogoutAsync(request.RefreshToken, cancellationToken);

        return NoContent();
    }

    /// <summary>
    /// Anonymous on purpose: the link is clicked in a browser with no session, so the token itself is
    /// the credential.
    /// </summary>
    [HttpPost("verify-email")]
    [AllowAnonymous]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> VerifyEmail(
        VerifyEmailRequest request,
        CancellationToken cancellationToken = default)
    {
        var outcome = await auth.VerifyEmailAsync(request.Token, cancellationToken);

        return outcome.Succeeded ? NoContent() : Failed(outcome.Error);
    }

    /// <summary>
    /// Always 204 — a registered, an unregistered, and a rate-limited address answer identically, or
    /// the endpoint is an account-existence oracle.
    /// </summary>
    [HttpPost("resend-verification")]
    [AllowAnonymous]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> ResendVerification(
        ResendVerificationRequest request,
        CancellationToken cancellationToken = default)
    {
        await auth.ResendVerificationAsync(request.Email, cancellationToken);

        return NoContent();
    }

    /// <summary>Always 204 — a registered and an unregistered address answer identically.</summary>
    [HttpPost("forgot-password")]
    [AllowAnonymous]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> ForgotPassword(
        ForgotPasswordRequest request,
        CancellationToken cancellationToken = default)
    {
        var remoteIp = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        await auth.ForgotPasswordAsync(request.Email, remoteIp, cancellationToken);

        return NoContent();
    }

    /// <summary>Revokes every session, including the one this reset creates for signing back in.</summary>
    [HttpPost("reset-password")]
    [AllowAnonymous]
    [ProducesResponseType<AuthSessionDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<AuthSessionDto>> ResetPassword(
        ResetPasswordRequest request,
        CancellationToken cancellationToken = default)
    {
        var outcome = await auth.ResetPasswordAsync(request.Token, request.NewPassword, cancellationToken);

        return outcome.Succeeded ? outcome.Session! : Failed(outcome.Error);
    }

    /// <summary>
    /// The account behind the bearer token, read from the database rather than from the token's own
    /// claims, so a client sees a renamed or demoted account without waiting for the token to expire.
    /// </summary>
    [HttpGet("me")]
    [Authorize]
    [ProducesResponseType<AuthUserDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<AuthUserDto>> Me(CancellationToken cancellationToken = default)
    {
        if (User.GetUserId() is not { } id)
        {
            return Unauthorized();
        }

        var user = await auth.GetUserAsync(id, cancellationToken);

        // The token is valid but the account is gone — deleted since it was issued.
        return user is null ? Unauthorized() : user;
    }

    /// <summary>
    /// Starts deleting the caller's own account: the typed name must be the account's, and a
    /// confirmation link is mailed. Nothing is deleted until that link is redeemed.
    /// </summary>
    [HttpPost("delete-account")]
    [Authorize]
    [ProducesResponseType(StatusCodes.Status202Accepted)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> RequestAccountDeletion(
        RequestAccountDeletionRequest request,
        CancellationToken cancellationToken = default)
    {
        if (User.GetUserId() is not { } id)
        {
            return Unauthorized();
        }

        var outcome = await auth.RequestAccountDeletionAsync(id, request.FullName, cancellationToken);
        return outcome.Succeeded ? Accepted() : Failed(outcome.Error);
    }

    /// <summary>
    /// Anonymous: the link is clicked from the mailbox. Deletes the account, or answers 409 with what
    /// the account holder still has to resolve when a plugged-in service's deletion guard objects.
    /// </summary>
    [HttpPost("confirm-account-deletion")]
    [AllowAnonymous]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> ConfirmAccountDeletion(
        ConfirmAccountDeletionRequest request,
        CancellationToken cancellationToken = default)
    {
        var (outcome, reason) = await auth.ConfirmAccountDeletionAsync(request.Token, cancellationToken);

        return outcome.Succeeded
            ? NoContent()
            : reason is not null
                ? Problem(statusCode: StatusCodes.Status409Conflict, detail: reason)
                : Failed(outcome.Error);
    }

    /// <summary>
    /// Rotates the caller's own session and revokes every other one — the device making the change
    /// stays signed in, everywhere else is signed out.
    /// </summary>
    [HttpPost("change-password")]
    [Authorize]
    [ProducesResponseType<AuthSessionDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<AuthSessionDto>> ChangePassword(
        ChangePasswordRequest request,
        CancellationToken cancellationToken = default)
    {
        if (User.GetUserId() is not { } id)
        {
            return Unauthorized();
        }

        var outcome = await auth.ChangePasswordAsync(
            id,
            request.CurrentPassword,
            request.NewPassword,
            request.RefreshToken,
            cancellationToken);

        return outcome.Succeeded ? outcome.Session! : Failed(outcome.Error);
    }

    /// <summary>
    /// Does not move the sign-in address yet — that happens at <c>confirm-email-change</c>, once the
    /// new address is confirmed.
    /// </summary>
    [HttpPost("change-email")]
    [Authorize]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> ChangeEmail(
        ChangeEmailRequest request,
        CancellationToken cancellationToken = default)
    {
        if (User.GetUserId() is not { } id)
        {
            return Unauthorized();
        }

        var outcome = await auth.ChangeEmailAsync(id, request.CurrentPassword, request.NewEmail, cancellationToken);

        return outcome.Succeeded ? NoContent() : Failed(outcome.Error);
    }

    /// <summary>Anonymous — the link is clicked at the new address, which has no session of its own.</summary>
    [HttpPost("confirm-email-change")]
    [AllowAnonymous]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> ConfirmEmailChange(
        ConfirmEmailChangeRequest request,
        CancellationToken cancellationToken = default)
    {
        var outcome = await auth.ConfirmEmailChangeAsync(request.Token, cancellationToken);

        return outcome.Succeeded ? NoContent() : Failed(outcome.Error);
    }

    /// <summary>
    /// Active sessions only, newest first. Pass the <c>SessionId</c> from the caller's own session to
    /// flag it as current — never a refresh token, which this route has no reason to see.
    /// </summary>
    [HttpGet("sessions")]
    [Authorize]
    [ProducesResponseType<PagedResult<AuthSessionSummaryDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<PagedResult<AuthSessionSummaryDto>>> GetSessions(
        [FromQuery] Guid? currentSessionId,
        [FromQuery] PageRequest paging,
        CancellationToken cancellationToken = default)
    {
        if (User.GetUserId() is not { } id)
        {
            return Unauthorized();
        }

        return Ok(await auth.GetSessionsAsync(id, currentSessionId, paging, cancellationToken));
    }

    /// <summary>
    /// Ends one session. 404 when the row is not the caller's — never confirm another user's session
    /// id exists. Does not sign out any other device.
    /// </summary>
    [HttpDelete("sessions/{id:guid}")]
    [Authorize]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteSession(Guid id, CancellationToken cancellationToken = default)
    {
        if (User.GetUserId() is not { } userId)
        {
            return Unauthorized();
        }

        var deleted = await auth.DeleteSessionAsync(userId, id, cancellationToken);

        return deleted ? NoContent() : NotFound();
    }

    /// <summary>Sign out everywhere, including the device making this call.</summary>
    [HttpPost("sessions/revoke-all")]
    [Authorize]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> RevokeAllSessions(CancellationToken cancellationToken = default)
    {
        if (User.GetUserId() is not { } id)
        {
            return Unauthorized();
        }

        await auth.RevokeAllSessionsAsync(id, cancellationToken);

        return NoContent();
    }

    private ActionResult Failed(AuthError error) => error switch
    {
        AuthError.EmailTaken => Problem(
            detail: "That email is already registered.",
            statusCode: StatusCodes.Status409Conflict),
        AuthError.RateLimited => Problem(
            detail: "Too many login attempts. Try again later.",
            statusCode: StatusCodes.Status429TooManyRequests),
        AuthError.InvalidToken => Problem(
            detail: "That link is invalid, expired, or already used.",
            statusCode: StatusCodes.Status400BadRequest),
        AuthError.NameMismatch => Problem(
            detail: "That is not the name on this account.",
            statusCode: StatusCodes.Status400BadRequest),
        AuthError.WeakPassword => Problem(
            detail: "The new password must be different from the current one.",
            statusCode: StatusCodes.Status400BadRequest),
        _ => Problem(
            detail: "Invalid credentials.",
            statusCode: StatusCodes.Status401Unauthorized)
    };
}
