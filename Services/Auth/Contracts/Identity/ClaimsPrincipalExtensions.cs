using System.Security.Claims;

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

    /// <summary>Whether the caller's current role grants <paramref name="permission"/>.</summary>
    public static bool HasPermission(this ClaimsPrincipal principal, string permission) =>
        principal.HasClaim(AuthClaims.Permission, permission);
}
