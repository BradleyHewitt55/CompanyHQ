using System.Text.Json.Serialization;
using CompanyHQ.Postgres.Contracts.Serialization;

namespace CompanyHQ.Sales.Contracts.Enums;

[JsonConverter(typeof(StrictStringEnumConverter<PaymentStatus>))]
public enum PaymentStatus
{
    Unpaid,
    Paid
}
