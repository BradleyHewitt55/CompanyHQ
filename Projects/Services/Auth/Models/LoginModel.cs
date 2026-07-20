using System.ComponentModel.DataAnnotations;

namespace CompanyHQ.Services.Auth.Models;

public sealed class LoginModel
{
    [Required, EmailAddress]
    public string Email { get; set; } = string.Empty;

    [Required]
    public string Password { get; set; } = string.Empty;
}
