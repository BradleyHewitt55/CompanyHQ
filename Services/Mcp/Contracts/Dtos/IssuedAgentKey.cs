using CompanyHQ.Mcp.Contracts.Enums;

namespace CompanyHQ.Mcp.Contracts.Dtos;

/// <summary>
/// The one moment the plaintext and the row coexist. Returned by create only — there is no "show key
/// again" endpoint and never will be.
/// </summary>
public sealed record IssuedAgentKeyDto(
    Guid Id,
    string Name,
    string Key,
    AgentKeyAccess Access,
    DateTimeOffset CreatedAt,
    DateTimeOffset? ExpiresAt,
    IReadOnlyList<string> McpDisabledTools,
    Guid? AccessRoleId = null);
