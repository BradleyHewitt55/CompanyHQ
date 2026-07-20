using System.ComponentModel.DataAnnotations;
using Abstractions.Users;

namespace CompanyHQ.Services.Auth.Models;

public sealed class AdminUserSaveModel
{
    public Guid? Id { get; set; }

    [Required]
    public string Name { get; set; } = string.Empty;

    [Required]
    public string Surname { get; set; } = string.Empty;

    [Required, EmailAddress]
    public string Email { get; set; } = string.Empty;

    [Required]
    public string ContactNumber { get; set; } = string.Empty;

    [Required]
    public string Role { get; set; } = string.Empty;

    [MinLength(PasswordPolicy.MinimumLength)]
    [MaxLength(PasswordPolicy.MaximumLength)]
    public string? Password { get; set; }

    public UserSave ToUserSave() => new(
        Id,
        Name,
        Surname,
        Email,
        ContactNumber,
        Role,
        Password);
}
