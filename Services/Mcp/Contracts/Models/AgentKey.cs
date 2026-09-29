using CompanyHQ.Services.Mcp.Contracts.Enums;
using CompanyHQ.Services.Postgres.Contracts.Models;

namespace CompanyHQ.Services.Mcp.Contracts.Models;

/// <summary>
/// A long-lived credential that authenticates at <c>/mcp</c> and nowhere else. The linked user's role
/// and email are read from Postgres on every call, never trusted from the key itself.
/// </summary>
public sealed class AgentKey : BaseModel
{
    public required Guid UserId { get; set; }

    /// <summary>User-supplied label, e.g. "Claude Desktop", "n8n inbox bot".</summary>
    public required string Name { get; set; }

    /// <summary>
    /// A SHA-256 digest of the key, never the key. Unsalted, same reasoning as
    /// <c>RefreshToken.TokenHash</c>: the row is found *by* this value and a 256-bit random key has
    /// no guessable input space.
    /// </summary>
    public required string KeyHash { get; set; }

    /// <summary>First 8 characters of the plaintext, stored in the clear, so the owner can tell keys
    /// apart in a list without either party being able to reconstruct the secret from it.</summary>
    public required string Prefix { get; set; }

    /// <summary>True for an OAuth access token; legacy <c>pgk_</c> keys remain supported.</summary>
    public bool IsOAuth { get; set; }

    /// <summary>The OAuth client this access token was issued to, never a client secret.</summary>
    public string? OAuthClientId { get; set; }

    /// <summary>The MCP resource audience this access token is bound to.</summary>
    public string? OAuthResource { get; set; }

    public AgentKeyAccess Access { get; set; }

    /// <summary>Optional role for this connection; when absent it uses the owner's assigned role.</summary>
    public Guid? AccessRoleId { get; set; }

    /// <summary>
    /// MCP tools this key explicitly disables. The linked user's role and access remain the upper
    /// bounds, so this list can only reduce what the key may discover and call.
    /// </summary>
    public string[] McpDisabledTools { get; set; } = [];

    public DateTimeOffset? ExpiresAt { get; set; }

    /// <summary>Written at most once per minute — see the Mcp plan's throttling note.</summary>
    public DateTimeOffset? LastUsedAt { get; set; }

    /// <summary>Kept, never deleted — same reasoning as <c>RefreshToken.RevokedAt</c>.</summary>
    public DateTimeOffset? RevokedAt { get; set; }

    /// <summary>Set only when an administrator revoked someone else's key.</summary>
    public Guid? RevokedByUserId { get; set; }

    public bool IsActive(DateTimeOffset now) =>
        RevokedAt is null && (ExpiresAt is null || ExpiresAt > now);
}
