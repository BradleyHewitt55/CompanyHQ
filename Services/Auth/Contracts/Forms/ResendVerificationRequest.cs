using System.ComponentModel.DataAnnotations;

namespace CompanyHQ.Services.Auth.Contracts.Forms;

public sealed record ResendVerificationRequest([Required, EmailAddress, MaxLength(320)] string Email);
