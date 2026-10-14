using System.ComponentModel.DataAnnotations;

using CompanyHQ.Moderation.Contracts.Enums;

namespace CompanyHQ.Moderation.Contracts.Forms;

/// <summary>Operator decision for a report already in the queue.</summary>
public sealed record ReportDecisionRequest(
    [EnumDataType(typeof(ReportStatus))] ReportStatus Status,
    [MaxLength(2000)] string? ReviewNote = null);
