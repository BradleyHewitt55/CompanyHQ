using System.ComponentModel.DataAnnotations;

namespace CompanyHQ.Auth.Contracts.Forms;

public sealed record ConfirmEmailChangeRequest([Required] string Token);
