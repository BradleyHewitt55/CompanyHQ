using System.ComponentModel.DataAnnotations;

namespace CompanyHQ.Services.Auth.Contracts.Forms;

public sealed record VerifyEmailRequest([Required] string Token);
