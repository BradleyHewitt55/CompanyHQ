namespace CompanyHQ.Auth.Contracts.Dtos;

/// <summary>
/// One of the caller's own active sessions, listed at <c>GET /api/auth/sessions</c> so they can end
/// one that isn't theirs anymore. No admin shape — there is no privileged view of your own sessions.
/// </summary>
public sealed record AuthSessionSummaryDto(
    Guid Id,
    string? DeviceLabel,
    DateTimeOffset CreatedAt,
    DateTimeOffset ExpiresAt,
    bool IsCurrent);
