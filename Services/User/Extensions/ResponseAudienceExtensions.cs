using System.Security.Claims;

using CompanyHQ.Services.Postgres.Contracts.Enums;
using CompanyHQ.Services.Users.Contracts.Enums;

namespace CompanyHQ.Services.Users.Extensions;

/// <summary>
/// Resolves which DTO shape a caller gets. Every controller that varies its response by role goes
/// through here rather than testing a claim by hand.
/// </summary>
/// <remarks>
/// Lives in this service, not in Auth, because the User service ships its own controller and Auth
/// already references it — the reverse arrow would be a cycle. It reads the role through
/// <see cref="ClaimsPrincipal.IsInRole"/>, the same mechanism <c>[Authorize(Roles = …)]</c> uses, so
/// the two can never disagree about who is an administrator.
/// </remarks>
public static class ResponseAudienceExtensions
{
    /// <summary>
    /// Anonymous callers and non-administrators get <see cref="ResponseAudience.Client"/>, so a
    /// missing or malformed token widens nothing.
    /// </summary>
    public static ResponseAudience Audience(this ClaimsPrincipal principal) =>
        principal.IsInRole(nameof(UserRole.Admin))
            ? ResponseAudience.Admin
            : principal.Identity?.IsAuthenticated is true
                ? ResponseAudience.Owner
                : ResponseAudience.Client;
}
