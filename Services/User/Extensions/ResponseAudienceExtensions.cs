using System.Security.Claims;

using CompanyHQ.Auth.Contracts.Identity;
using CompanyHQ.Postgres.Contracts.Enums;

namespace CompanyHQ.Users.Extensions;

/// <summary>
/// Resolves which DTO shape a caller gets. Every controller that varies its response by access goes
/// through here rather than testing a claim by hand.
/// </summary>
public static class ResponseAudienceExtensions
{
    /// <summary>
    /// The operator shape only for a caller whose role grants <paramref name="permission"/>. Anyone
    /// else signed in gets <see cref="ResponseAudience.Owner"/> and an anonymous caller
    /// <see cref="ResponseAudience.Client"/>, so a missing or malformed token widens nothing.
    /// </summary>
    public static ResponseAudience Audience(this ClaimsPrincipal principal, string permission) =>
        principal.HasPermission(permission)
            ? ResponseAudience.Admin
            : principal.Identity?.IsAuthenticated is true
                ? ResponseAudience.Owner
                : ResponseAudience.Client;
}
