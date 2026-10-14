using System.ComponentModel.DataAnnotations;

namespace CompanyHQ.Auth.Contracts.Forms;

public sealed record ConfirmAccountDeletionRequest([Required] string Token);
