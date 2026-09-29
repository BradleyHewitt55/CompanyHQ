using CompanyHQ.Services.Auth.Contracts.Models;
using CompanyHQ.Services.Auth.Services;
using CompanyHQ.Services.Email.Contracts.Ports;

namespace Api.Integrations;

/// <summary>Fills Auth's <see cref="IAuthEmailSender"/> with Email's controlled templates.</summary>
internal sealed class AuthEmailSender(
    IEmailTemplateSender templates,
    AuthTokenOptions tokenOptions,
    ILogger<AuthEmailSender> logger) : IAuthEmailSender
{
    public Task SendVerificationEmailAsync(
        string email,
        IssuedAuthToken token,
        CancellationToken cancellationToken = default) =>
        SendAsync(
            email,
            EmailTemplateKeys.VerifyEmail,
            new Dictionary<string, string?> { ["link"] = Link("verify-email", token.Value) },
            $"auth:email-verification:{token.TokenId}",
            cancellationToken);

    public Task SendPasswordResetEmailAsync(
        string email,
        IssuedAuthToken token,
        CancellationToken cancellationToken = default) =>
        SendAsync(
            email,
            EmailTemplateKeys.PasswordReset,
            new Dictionary<string, string?> { ["link"] = Link("reset-password", token.Value) },
            $"auth:password-reset:{token.TokenId}",
            cancellationToken);

    public Task SendEmailChangeConfirmationAsync(
        string newEmail,
        IssuedAuthToken token,
        CancellationToken cancellationToken = default) =>
        SendAsync(
            newEmail,
            EmailTemplateKeys.EmailChangeConfirmation,
            new Dictionary<string, string?> { ["link"] = Link("confirm-email-change", token.Value) },
            $"auth:email-change:{token.TokenId}",
            cancellationToken);

    public Task SendPasswordChangedNoticeAsync(
        Guid userId,
        string email,
        DateTimeOffset changedAt,
        CancellationToken cancellationToken = default) =>
        SendAsync(
            email,
            EmailTemplateKeys.PasswordChanged,
            new Dictionary<string, string?>(),
            $"auth:password-changed:{userId}:{changedAt:O}",
            cancellationToken);

    public Task SendEmailChangingNoticeAsync(
        Guid userId,
        string email,
        string newEmail,
        DateTimeOffset requestedAt,
        CancellationToken cancellationToken = default) =>
        SendAsync(
            email,
            EmailTemplateKeys.EmailChanging,
            new Dictionary<string, string?> { ["newEmail"] = newEmail },
            $"auth:email-changing:{userId}:{requestedAt:O}",
            cancellationToken);

    // Best-effort by IAuthEmailSender's contract: a failed queue is logged, never thrown.
    private async Task SendAsync(
        string recipient,
        string templateKey,
        IReadOnlyDictionary<string, string?> values,
        string idempotencyKey,
        CancellationToken cancellationToken)
    {
        var result = await templates.SendAsync([recipient], templateKey, values, idempotencyKey, cancellationToken);
        if (!result.IsSuccess)
        {
            logger.LogWarning("Auth email {IdempotencyKey} was not queued.", idempotencyKey);
        }
    }

    private string Link(string path, string token) =>
        $"{tokenOptions.WebBaseAddress.TrimEnd('/')}/auth/{path}?token={Uri.EscapeDataString(token)}";
}
