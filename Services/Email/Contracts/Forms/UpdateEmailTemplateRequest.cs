using System.ComponentModel.DataAnnotations;

namespace CompanyHQ.Services.Email.Contracts.Forms;

public sealed record UpdateEmailTemplateRequest(
    [Required, MaxLength(200_000)] string Html);
