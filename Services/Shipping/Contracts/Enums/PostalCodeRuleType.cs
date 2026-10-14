using System.Text.Json.Serialization;
using CompanyHQ.Postgres.Contracts.Serialization;

namespace CompanyHQ.Shipping.Contracts.Enums;

/// <summary>How a delivery zone rule reads its pattern.</summary>
[JsonConverter(typeof(StrictStringEnumConverter<PostalCodeRuleType>))]
public enum PostalCodeRuleType
{
    /// <summary>The whole code, without case.</summary>
    Exact,

    /// <summary><c>from-to</c>, both whole numbers, inclusive.</summary>
    Range,

    /// <summary><c>*</c> stands for any run of characters, so <c>75*</c> covers every code starting 75.</summary>
    Wildcard
}
