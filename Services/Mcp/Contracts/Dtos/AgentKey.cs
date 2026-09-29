using System.Text.Json.Serialization;

using CompanyHQ.Services.Mcp.Contracts.Enums;

namespace CompanyHQ.Services.Mcp.Contracts.Dtos;

/// <summary>An owner's own key. Never the hash — the plaintext existed exactly once, at issue.</summary>
[JsonDerivedType(typeof(AdminAgentKeyDto))]
public record ClientAgentKeyDto(
    Guid Id,
    string Name,
    string Prefix,
    AgentKeyAccess Access,
    DateTimeOffset CreatedAt,
    DateTimeOffset? LastUsedAt,
    DateTimeOffset? ExpiresAt,
    DateTimeOffset? RevokedAt,
    IReadOnlyList<string> McpDisabledTools,
    Guid? AccessRoleId = null);

/// <summary>The operator's view: whose key it is, and who revoked it when that wasn't the owner.</summary>
public sealed record AdminAgentKeyDto(
    Guid Id,
    string Name,
    string Prefix,
    AgentKeyAccess Access,
    DateTimeOffset CreatedAt,
    DateTimeOffset? LastUsedAt,
    DateTimeOffset? ExpiresAt,
    DateTimeOffset? RevokedAt,
    Guid UserId,
    string? UserFirstName,
    string? UserLastName,
    Guid? RevokedByUserId,
    IReadOnlyList<string> McpDisabledTools,
    Guid? AccessRoleId = null)
    : ClientAgentKeyDto(Id, Name, Prefix, Access, CreatedAt, LastUsedAt, ExpiresAt, RevokedAt, McpDisabledTools, AccessRoleId);
