using CompanyHQ.Mcp.Contracts.Dtos;
using CompanyHQ.Mcp.Contracts.Forms;
using CompanyHQ.Postgres.Contracts.Dtos;
using CompanyHQ.Postgres.Contracts.Forms;

namespace CompanyHQ.Mcp.Services;

/// <summary>Issues, lists, and revokes agent keys. Never re-displays a plaintext after issue.</summary>
public interface IAgentKeyService
{
    Task<AgentKeyOutcome> IssueAsync(
        Guid userId,
        IssueAgentKeyRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>The caller's own not-yet-revoked keys, newest first.</summary>
    Task<PagedResult<ClientAgentKeyDto>> ListMineAsync(
        Guid userId,
        PageRequest paging,
        CancellationToken cancellationToken = default);

    /// <summary>An administrator's view of another user's keys — the admin gate lives here, not the controller.</summary>
    Task<PagedResult<AdminAgentKeyDto>> ListForUserAsync(
        Guid targetUserId,
        PageRequest paging,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Revokes a key. False when it does not exist, is already revoked, or belongs to someone else
    /// and the caller is not an administrator — the same answer for all three, so a caller cannot
    /// use this to learn whether a key id exists.
    /// </summary>
    Task<bool> RevokeAsync(
        Guid callerId,
        bool callerIsAdmin,
        Guid keyId,
        CancellationToken cancellationToken = default);

    Task<bool> AssignRoleAsync(Guid callerId, bool canManageUsers, Guid keyId,
        Guid? accessRoleId, CancellationToken cancellationToken = default);
}
