using System.Text.Json.Serialization;
using CompanyHQ.Postgres.Contracts.Serialization;

namespace CompanyHQ.Sales.Contracts.Enums;

[JsonConverter(typeof(StrictStringEnumConverter<QuoteStatus>))]
public enum QuoteStatus
{
    Draft,
    Sent,
    Accepted,
    Expired,
    Converted,
    /// <summary>The immutable issued revision is reserved; delivery may safely be retried.</summary>
    Issuing
}
