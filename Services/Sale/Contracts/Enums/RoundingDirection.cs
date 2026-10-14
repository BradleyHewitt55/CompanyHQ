using System.Text.Json.Serialization;
using CompanyHQ.Services.Postgres.Contracts.Serialization;

namespace CompanyHQ.Services.Sales.Contracts.Enums;

/// <summary>Which way a price moves to reach its rounding target.</summary>
[JsonConverter(typeof(StrictStringEnumConverter<RoundingDirection>))]
public enum RoundingDirection
{
    RoundUp,
    RoundDown
}
