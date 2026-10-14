using CompanyHQ.Persistence.Contracts.Models;

namespace CompanyHQ.Email.Contracts.Models;

/// <summary>An administrator-managed override for one controlled transactional email template.</summary>
public sealed class EmailTemplate : BaseModel
{
    public required string Key { get; set; }

    public required string DisplayName { get; set; }

    public required string Html { get; set; }

    public Guid? UpdatedByUserId { get; set; }
}
