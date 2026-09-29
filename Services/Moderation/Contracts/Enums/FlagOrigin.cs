using System.Text.Json.Serialization;

using CompanyHQ.Services.Postgres.Contracts.Serialization;

namespace CompanyHQ.Services.Moderation.Contracts.Enums;

/// <summary>Whether an account flag was raised by a detector or an operator.</summary>
[JsonConverter(typeof(StrictStringEnumConverter<FlagOrigin>))]
public enum FlagOrigin
{
    System,
    Operator
}
