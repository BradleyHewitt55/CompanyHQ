using CompanyHQ.Services.Auth.Contracts.Enums;
using CompanyHQ.Services.Auth.Contracts.Models;

namespace CompanyHQ.Services.Auth.Services;

/// <summary>
/// Issues, consumes, and invalidates the single-use tokens behind password reset, email
/// verification, and email change. It reads no email template and sends nothing — that is the
/// caller's business, this is only the credential lifecycle.
/// </summary>
public interface IAuthTokenService
{
    /// <summary>
    /// Issues a token of <paramref name="purpose"/> for <paramref name="userId"/>, first consuming
    /// every other open token of the same purpose for that user — one live link per purpose, always
    /// the newest.
    /// </summary>
    Task<IssuedAuthToken> IssueAsync(
        Guid userId,
        AuthTokenPurpose purpose,
        string? pendingEmail = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Consumes a token by its plaintext value, or returns null when it is unknown, the wrong
    /// purpose, expired, or already consumed. Stamps <see cref="AuthToken.ConsumedAt"/> rather than
    /// deleting the row, so a repeat click reads as "already used" instead of a mystery 400.
    /// </summary>
    Task<AuthToken?> ConsumeAsync(
        string token,
        AuthTokenPurpose purpose,
        CancellationToken cancellationToken = default);

    /// <summary>Consumes every open token of <paramref name="purpose"/> for a user without minting a new one.</summary>
    /// <summary>
    /// The open token for <paramref name="purpose"/>, left unconsumed. For an action that deletes
    /// the token's own account: the row goes with the account, and a refused action can be retried
    /// from the same link.
    /// </summary>
    Task<AuthToken?> FindActiveAsync(
        string token,
        AuthTokenPurpose purpose,
        CancellationToken cancellationToken = default);

    Task InvalidateAsync(Guid userId, AuthTokenPurpose purpose, CancellationToken cancellationToken = default);
}
