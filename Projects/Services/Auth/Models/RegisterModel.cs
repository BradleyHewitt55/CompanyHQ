using System.ComponentModel.DataAnnotations;
using Abstractions.Users;

namespace CompanyHQ.Services.Auth.Models;

public sealed class RegisterModel
{
    [Required]
    public string Name { get; set; } = string.Empty;

    [Required]
    public string Surname { get; set; } = string.Empty;

    [Required, EmailAddress]
    public string Email { get; set; } = string.Empty;

    [Required]
    public string ContactNumber { get; set; } = string.Empty;

    [Required]
    [MinLength(PasswordPolicy.MinimumLength)]
    [MaxLength(PasswordPolicy.MaximumLength)]
    public string Password { get; set; } = string.Empty;

    [Required, Compare(nameof(Password))]
    public string Confirmation { get; set; } = string.Empty;
}
