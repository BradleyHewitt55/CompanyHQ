using System.Text.Json.Serialization;

using CompanyHQ.Services.Postgres.Contracts.Serialization;

namespace CompanyHQ.Services.Moderation.Contracts.Enums;

/// <summary>The independent owner of a caller-specific hidden-content row.</summary>
[JsonConverter(typeof(StrictStringEnumConverter<HiddenContentSource>))]
public enum HiddenContentSource
{
    Legacy,
    Manual,
    Report,
    Block
}
