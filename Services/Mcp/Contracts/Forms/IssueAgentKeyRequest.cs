using System.ComponentModel.DataAnnotations;

using CompanyHQ.Services.Mcp.Contracts.Enums;

namespace CompanyHQ.Services.Mcp.Contracts.Forms;

public sealed record IssueAgentKeyRequest(
    [Required, MaxLength(60)] string Name,
    AgentKeyAccess Access = AgentKeyAccess.ReadOnly,
    DateTimeOffset? ExpiresAt = null,
    IReadOnlyList<string>? McpDisabledTools = null,
    Guid? AccessRoleId = null);
