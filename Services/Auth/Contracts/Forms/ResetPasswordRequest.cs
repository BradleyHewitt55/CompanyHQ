using System.ComponentModel.DataAnnotations;

namespace CompanyHQ.Auth.Contracts.Forms;

public sealed record ResetPasswordRequest(
    [Required] string Token,
    [Required, MinLength(12), MaxLength(256)] string NewPassword);
