using CompanyHQ.Services.Postgres.Contracts.Models;

using CompanyHQ.Services.Users.Contracts.Enums;
namespace CompanyHQ.Services.Users.Contracts.Models;

public sealed class User : BaseModel
{
    public required string Email { get; set; }

    public required string PasswordHash { get; set; }

    public UserRole Role { get; set; } = UserRole.Client;

    /// <summary>The saved role used for current authorization. Null is a legacy account.</summary>
    public Guid? AccessRoleId { get; set; }
    public AccessRole? AccessRole { get; set; }

    public string? FirstName { get; set; }

    public string? LastName { get; set; }

    public string? Bio { get; set; }

    public string? ProfileImageUrl { get; set; }

    /// <summary>Set once the address is confirmed. Gates outbound-to-strangers actions such as message send.</summary>
    public bool EmailVerified { get; set; }

    /// <summary>
    /// MCP tools this account has explicitly disabled. The backend role remains the upper bound:
    /// removing a name from this list never grants a role-denied capability.
    /// </summary>
    public string[] McpDisabledTools { get; set; } = [];

    /// <summary>
    /// Stamped by a global session revocation (reset-password, change-password, confirm-email-change,
    /// sessions/revoke-all). An access token minted before this instant fails validation even though
    /// its signature is still good — this is what closes the access-token blast radius those flows
    /// would otherwise leave open for up to fifteen minutes.
    /// </summary>
    public DateTimeOffset? SessionsRevokedAt { get; set; }
}
