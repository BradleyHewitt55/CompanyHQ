using CompanyHQ.Persistence.Contracts.Models;

namespace CompanyHQ.Mcp.Contracts.Models;

/// <summary>
/// A short-lived, single-use OAuth authorization code. Only its hash is persisted; the code itself
/// exists in memory long enough to redirect the client to its callback.
/// </summary>
public sealed class McpAuthorizationCode : BaseModel
{
    public required Guid UserId { get; set; }

    public required string CodeHash { get; set; }

    public required string ClientId { get; set; }

    public required string RedirectUri { get; set; }

    public required string Resource { get; set; }

    public required string CodeChallenge { get; set; }

    public DateTimeOffset ExpiresAt { get; set; }

    public DateTimeOffset? ConsumedAt { get; set; }

    public bool IsActive(DateTimeOffset now) => ConsumedAt is null && ExpiresAt > now;
}
