using System.ComponentModel.DataAnnotations;

namespace CompanyHQ.Services.Email.Contracts.Forms;

public sealed record PreviewEmailTemplateRequest(
    [Required, MaxLength(200_000)] string Html,
    IReadOnlyDictionary<string, string>? Values = null);
