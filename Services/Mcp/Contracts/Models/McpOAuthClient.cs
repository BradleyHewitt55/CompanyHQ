using CompanyHQ.Services.Persistence.Contracts.Models;

namespace CompanyHQ.Services.Mcp.Contracts.Models;

/// <summary>Public OAuth client registration metadata. No client secret is issued or stored.</summary>
public sealed class McpOAuthClient : BaseModel
{
    public required string ClientId { get; set; }

    public required string[] RedirectUris { get; set; }

    public required string[] GrantTypes { get; set; }

    public string? ClientName { get; set; }
}
