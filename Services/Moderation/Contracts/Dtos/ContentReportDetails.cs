using CompanyHQ.Services.Moderation.Contracts.Enums;

namespace CompanyHQ.Services.Moderation.Contracts.Dtos;

/// <summary>The queue shape plus frozen evidence for an operator's detail view.</summary>
public sealed record AdminContentReportDetailsDto(
    Guid Id,
    string TargetKind,
    Guid TargetId,
    ReportReason Reason,
    ReportStatus Status,
    DateTimeOffset CreatedAt,
    Guid ReporterUserId,
    Guid? TargetOwnerId,
    string? Details,
    Guid? ReviewedByUserId,
    DateTimeOffset? ReviewedAt,
    string? ReviewNote,
    int ReportCountOnTarget,
    bool IsGloballyRestricted,
    IReadOnlyList<AdminLinkedContentReportDto> Reports,
    IReadOnlyList<AdminReportedContentSnapshotDto> Snapshots,
    IReadOnlyList<AdminReportDecisionDto> DecisionHistory)
    : AdminContentReportDto(
        Id,
        TargetKind,
        TargetId,
        Reason,
        Status,
        CreatedAt,
        ReporterUserId,
        TargetOwnerId,
        Details,
        ReviewedByUserId,
        ReviewedAt,
        ReviewNote,
        ReportCountOnTarget,
        IsGloballyRestricted);

/// <summary>One persisted allegation linked to the same reported target.</summary>
public sealed record AdminLinkedContentReportDto(
    Guid Id,
    Guid ReporterUserId,
    ReportReason Reason,
    string? Details,
    ReportStatus Status,
    DateTimeOffset CreatedAt);
