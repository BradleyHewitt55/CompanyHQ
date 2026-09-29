using System.Text.Json.Serialization;

using CompanyHQ.Services.Postgres.Contracts;
using CompanyHQ.Services.Users.Contracts.Enums;

namespace CompanyHQ.Services.Users.Contracts.Dtos;

/// <summary>Public profile content without account identifiers, contact details, or role.</summary>
[JsonDerivedType(typeof(OwnerUserDto))]
[JsonDerivedType(typeof(AdminUserDto))]
public record ClientUserDto(
    string? FirstName,
    string? LastName,
    string? Bio,
    string? ProfileImageUrl,
    DateTimeOffset CreatedAt);

/// <summary>An authenticated caller's view, which adds the account identifier.</summary>
public record OwnerUserDto(
    Guid Id,
    string? FirstName,
    string? LastName,
    string? Bio,
    string? ProfileImageUrl,
    DateTimeOffset CreatedAt)
    : ClientUserDto(FirstName, LastName, Bio, ProfileImageUrl, CreatedAt);

/// <summary>The operator's account view: identifier, contact address, role, and audit stamp.</summary>
public sealed record AdminUserDto(
    Guid Id,
    string Email,
    string? FirstName,
    string? LastName,
    string? Bio,
    string? ProfileImageUrl,
    UserRole Role,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    string[] McpDisabledTools,
    Guid? AccessRoleId = null,
    string? RoleName = null,
    IReadOnlyList<string>? RolePermissions = null)
    : OwnerUserDto(Id, FirstName, LastName, Bio, ProfileImageUrl, CreatedAt)
{
    /// <summary>Effective tools after the backend role and account deny list are intersected.</summary>
    public IReadOnlyList<string> McpAllowedTools =>
        RolePermissions is null
            ? McpCapabilityCatalog.Effective(Role.ToString(), McpDisabledTools)
            : RolePermissions.Where(permission => McpCapabilityCatalog.IsKnown(permission)
                && !McpDisabledTools.Contains(permission, StringComparer.Ordinal)).ToArray();
}
