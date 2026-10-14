using CompanyHQ.Persistence.Contracts.Models;

namespace CompanyHQ.Moderation.Contracts.Models;

/// <summary>Frozen report evidence copied from a consumer at report time.</summary>
public sealed class ReportedContentSnapshot : BaseModel
{
    public Guid ContentReportId { get; set; }

    public Guid? AuthorUserId { get; set; }

    public required string Body { get; set; }

    public DateTimeOffset OccurredAt { get; set; }

    public bool IsTarget { get; set; }
}
