using CompanyHQ.Services.Moderation.Contracts.Enums;
using CompanyHQ.Services.Postgres.Contracts.Models;

namespace CompanyHQ.Services.Moderation.Contracts.Models;

/// <summary>Append-only history for one accepted report status transition.</summary>
public sealed class ContentReportDecision : BaseModel
{
    public Guid ContentReportId { get; set; }

    public Guid AdministratorUserId { get; set; }

    public ReportStatus PreviousStatus { get; set; }

    public ReportStatus NewStatus { get; set; }

    public string? Note { get; set; }

    public DateTimeOffset DecidedAt { get; set; }
}
