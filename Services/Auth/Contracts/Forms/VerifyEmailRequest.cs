using System.ComponentModel.DataAnnotations;

namespace CompanyHQ.Auth.Contracts.Forms;

public sealed record VerifyEmailRequest([Required] string Token);
