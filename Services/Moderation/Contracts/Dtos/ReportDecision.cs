using CompanyHQ.Services.Moderation.Contracts.Enums;

namespace CompanyHQ.Services.Moderation.Contracts.Dtos;

/// <summary>One immutable operator decision in a report's append-only history.</summary>
public sealed record AdminReportDecisionDto(
    Guid AdministratorUserId,
    ReportStatus PreviousStatus,
    ReportStatus NewStatus,
    string? Note,
    DateTimeOffset DecidedAt);
