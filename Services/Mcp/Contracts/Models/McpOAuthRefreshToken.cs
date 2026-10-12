using CompanyHQ.Services.Persistence.Contracts.Models;

namespace CompanyHQ.Services.Mcp.Contracts.Models;

/// <summary>One rotating OAuth refresh token. Only its hash is persisted.</summary>
public sealed class McpOAuthRefreshToken : BaseModel
{
    public required Guid UserId { get; set; }

    public required string TokenHash { get; set; }

    public required string ClientId { get; set; }

    public required string Resource { get; set; }

    public DateTimeOffset ExpiresAt { get; set; }

    public DateTimeOffset? RevokedAt { get; set; }

    public bool IsActive(DateTimeOffset now) => RevokedAt is null && ExpiresAt > now;
}
