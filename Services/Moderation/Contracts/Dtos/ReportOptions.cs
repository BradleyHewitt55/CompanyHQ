using CompanyHQ.Moderation.Contracts.Enums;

namespace CompanyHQ.Moderation.Contracts.Dtos;

/// <summary>Server-authoritative report reasons for one normalized target kind.</summary>
public sealed record ReportOptionsDto(
    string TargetKind,
    IReadOnlyList<ReportReasonOptionDto> Reasons);

/// <summary>One report reason and whether the caller must supply details.</summary>
public sealed record ReportReasonOptionDto(
    ReportReason Reason,
    bool DetailsRequired);
