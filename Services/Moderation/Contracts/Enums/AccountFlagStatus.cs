using System.Text.Json.Serialization;

using CompanyHQ.Services.Postgres.Contracts.Serialization;

namespace CompanyHQ.Services.Moderation.Contracts.Enums;

/// <summary>The review lifecycle of an account flag.</summary>
[JsonConverter(typeof(StrictStringEnumConverter<AccountFlagStatus>))]
public enum AccountFlagStatus
{
    Active,
    Cleared,
    Upheld
}
