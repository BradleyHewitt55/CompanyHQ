using System.ComponentModel.DataAnnotations;

using CompanyHQ.Users.Contracts.Enums;
namespace CompanyHQ.Users.Contracts.Forms;

/// <summary>
/// Body for <c>PUT /api/users/{id}</c>. Every field is optional; omitting one leaves the stored
/// value alone, so a caller editing a name cannot blank out a role by accident.
/// </summary>
public sealed record UpdateUserRequest(
    [EmailAddress, MaxLength(320)] string? Email = null,
    [MaxLength(60)] string? FirstName = null,
    [MaxLength(60)] string? LastName = null,
    [MaxLength(500)] string? Bio = null,
    [EnumDataType(typeof(UserRole))] UserRole? Role = null,
    [MinLength(12), MaxLength(256)] string? Password = null,
    IReadOnlyList<string>? McpDisabledTools = null,
    [MaxLength(80)] string? RoleName = null);
