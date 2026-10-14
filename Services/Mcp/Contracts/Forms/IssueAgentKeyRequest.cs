using System.ComponentModel.DataAnnotations;

using CompanyHQ.Mcp.Contracts.Enums;

namespace CompanyHQ.Mcp.Contracts.Forms;

public sealed record IssueAgentKeyRequest(
    [Required, MaxLength(60)] string Name,
    AgentKeyAccess Access = AgentKeyAccess.ReadOnly,
    DateTimeOffset? ExpiresAt = null,
    IReadOnlyList<string>? McpDisabledTools = null,
    Guid? AccessRoleId = null);
