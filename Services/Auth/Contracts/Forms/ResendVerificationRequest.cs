using System.ComponentModel.DataAnnotations;

namespace CompanyHQ.Auth.Contracts.Forms;

public sealed record ResendVerificationRequest([Required, EmailAddress, MaxLength(320)] string Email);
