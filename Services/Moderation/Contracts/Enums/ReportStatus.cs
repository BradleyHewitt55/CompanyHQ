using System.Text.Json.Serialization;

using CompanyHQ.Services.Postgres.Contracts.Serialization;

namespace CompanyHQ.Services.Moderation.Contracts.Enums;

/// <summary>The review lifecycle of one caller's report.</summary>
[JsonConverter(typeof(StrictStringEnumConverter<ReportStatus>))]
public enum ReportStatus
{
    Pending,
    Reviewed,
    ActionTaken,
    Dismissed,
    Withdrawn
}
