using System.Text.Json.Serialization;

using CompanyHQ.Services.Postgres.Contracts.Serialization;

namespace CompanyHQ.Services.Moderation.Contracts.Enums;

/// <summary>The reason a caller believes a target violates CompanyHQ's rules.</summary>
[JsonConverter(typeof(StrictStringEnumConverter<ReportReason>))]
public enum ReportReason
{
    Irrelevant,
    Spam,
    Harassment,
    Impersonation,
    SexualContent,
    Violence,
    Misinformation,
    Other
}
