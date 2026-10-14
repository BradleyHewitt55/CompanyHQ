using System.ComponentModel.DataAnnotations;

using CompanyHQ.Users.Contracts.Enums;
namespace CompanyHQ.Users.Contracts.Forms;

/// <summary>
/// Body for <c>POST /api/users</c>. The password arrives in plaintext and is hashed before it
/// reaches the database — it is never stored or logged.
/// </summary>
/// <remarks>
/// Validation attributes sit on the constructor parameters, not the generated properties: MVC
/// rejects a record whose validation metadata is on the properties.
/// </remarks>
public sealed record CreateUserRequest(
    [Required, EmailAddress, MaxLength(320)] string Email,
    [Required, MinLength(12), MaxLength(256)] string Password,
    [MaxLength(60)] string? FirstName = null,
    [MaxLength(60)] string? LastName = null,
    UserRole Role = UserRole.Client,
    [MaxLength(500)] string? Bio = null,
    IReadOnlyList<string>? McpDisabledTools = null,
    [MaxLength(80)] string? RoleName = null);
