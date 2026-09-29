using System.ComponentModel.DataAnnotations;

using CompanyHQ.Services.Moderation.Contracts.Enums;

namespace CompanyHQ.Services.Moderation.Contracts.Forms;

/// <summary>Body for filing a report and optionally clearing the reporter's chat history.</summary>
public sealed record CreateReportRequest(
    [Required, MinLength(1), MaxLength(32)] string TargetKind,
    Guid TargetId,
    [EnumDataType(typeof(ReportReason))] ReportReason Reason,
    [MaxLength(1000)] string? Details = null,
    bool ClearHistory = false);
