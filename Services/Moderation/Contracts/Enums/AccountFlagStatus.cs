using System.Text.Json.Serialization;

using CompanyHQ.Postgres.Contracts.Serialization;

namespace CompanyHQ.Moderation.Contracts.Enums;

/// <summary>The review lifecycle of an account flag.</summary>
[JsonConverter(typeof(StrictStringEnumConverter<AccountFlagStatus>))]
public enum AccountFlagStatus
{
    Active,
    Cleared,
    Upheld
}
