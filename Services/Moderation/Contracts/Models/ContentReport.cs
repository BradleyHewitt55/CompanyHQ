using CompanyHQ.Services.Moderation.Contracts.Enums;
using CompanyHQ.Services.Postgres.Contracts.Models;

namespace CompanyHQ.Services.Moderation.Contracts.Models;

/// <summary>One caller's report of one target and its operator review state.</summary>
public sealed class ContentReport : BaseModel
{
    public Guid ReporterUserId { get; set; }

    public required string TargetKind { get; set; }

    public Guid TargetId { get; set; }

    public Guid? TargetOwnerId { get; set; }

    public ReportReason Reason { get; set; }

    public string? Details { get; set; }

    public ReportStatus Status { get; set; } = ReportStatus.Pending;

    public Guid? ReviewedByUserId { get; set; }

    public DateTimeOffset? ReviewedAt { get; set; }

    public string? ReviewNote { get; set; }
}
