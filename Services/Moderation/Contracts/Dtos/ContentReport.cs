using System.Text.Json.Serialization;

using CompanyHQ.Services.Moderation.Contracts.Enums;

namespace CompanyHQ.Services.Moderation.Contracts.Dtos;

/// <summary>The narrow report receipt returned to the person who filed it.</summary>
[JsonDerivedType(typeof(AdminContentReportDto))]
[JsonDerivedType(typeof(AdminContentReportDetailsDto))]
public record ClientContentReportDto(
    Guid Id,
    string TargetKind,
    Guid TargetId,
    ReportReason Reason,
    ReportStatus Status,
    DateTimeOffset CreatedAt);

/// <summary>The moderation queue shape, including identities and review fields.</summary>
public record AdminContentReportDto(
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
    bool IsGloballyRestricted)
    : ClientContentReportDto(Id, TargetKind, TargetId, Reason, Status, CreatedAt);
