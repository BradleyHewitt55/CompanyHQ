using System.ComponentModel.DataAnnotations;

namespace CompanyHQ.Auth.Contracts.Forms;

public sealed record ForgotPasswordRequest([Required, EmailAddress, MaxLength(320)] string Email);
