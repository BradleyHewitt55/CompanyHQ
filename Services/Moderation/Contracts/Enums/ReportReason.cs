using System.Text.Json.Serialization;

using CompanyHQ.Postgres.Contracts.Serialization;

namespace CompanyHQ.Moderation.Contracts.Enums;

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
