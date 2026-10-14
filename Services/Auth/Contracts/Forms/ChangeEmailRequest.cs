using System.ComponentModel.DataAnnotations;

namespace CompanyHQ.Auth.Contracts.Forms;

public sealed record ChangeEmailRequest(
    [Required] string CurrentPassword,
    [Required, EmailAddress, MaxLength(320)] string NewEmail);
