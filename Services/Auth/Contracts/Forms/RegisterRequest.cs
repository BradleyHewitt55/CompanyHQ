using System.ComponentModel.DataAnnotations;

namespace CompanyHQ.Auth.Contracts.Forms;

/// <summary>
/// Body for <c>POST /api/auth/register</c>. Self-service signup, so the role is not a field —
/// every account created here is a client. Promotion goes through <c>PUT /api/users/{id}</c>.
/// </summary>
/// <remarks>
/// Validation attributes sit on the constructor parameters, not the generated properties: MVC
/// rejects a record whose validation metadata is on the properties.
/// </remarks>
public sealed record RegisterRequest(
    [Required, EmailAddress, MaxLength(320)] string Email,
    [Required, MinLength(12), MaxLength(256)] string Password,
    [MaxLength(60)] string? FirstName = null,
    [MaxLength(60)] string? LastName = null,
    [MaxLength(500)] string? Bio = null);
