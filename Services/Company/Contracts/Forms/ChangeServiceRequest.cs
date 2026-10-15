using System.ComponentModel.DataAnnotations;

namespace CompanyHQ.Company.Contracts.Forms;

public sealed record ChangeServiceRequest(bool Enabled, [MaxLength(80)] string? SelectedProvider = null);
