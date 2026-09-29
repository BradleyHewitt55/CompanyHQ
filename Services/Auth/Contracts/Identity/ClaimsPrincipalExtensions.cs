using System.Security.Claims;

using CompanyHQ.Services.Users.Contracts.Enums;

namespace CompanyHQ.Services.Auth.Contracts.Identity;

/// <summary>
/// Reads CompanyHQ's claims off the caller. Every endpoint that needs "who is this" goes through
/// here rather than indexing claims by hand; it lives in the dependency-free contracts so any service
/// can use it without referencing Auth itself.
/// </summary>
public static class ClaimsPrincipalExtensions
{
    /// <summary>Null when unauthenticated, or when the subject claim is not a GUID.</summary>
    public static Guid? GetUserId(this ClaimsPrincipal principal) =>
        Guid.TryParse(principal.FindFirst(AuthClaims.Subject)?.Value, out var id) ? id : null;

    public static string? GetEmail(this ClaimsPrincipal principal) =>
        principal.FindFirst(AuthClaims.Email)?.Value;

    public static UserRole? GetRole(this ClaimsPrincipal principal) =>
        Enum.TryParse<UserRole>(principal.FindFirst(AuthClaims.Role)?.Value, out var role) ? role : null;

    public static bool GetEmailVerified(this ClaimsPrincipal principal) =>
        bool.TryParse(principal.FindFirst(AuthClaims.EmailVerified)?.Value, out var verified) && verified;
}
