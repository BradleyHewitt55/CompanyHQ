using System.Text.Json.Serialization;
using CompanyHQ.Services.Postgres.Contracts.Serialization;

namespace CompanyHQ.Services.Sales.Contracts.Enums;

[JsonConverter(typeof(StrictStringEnumConverter<PaymentStatus>))]
public enum PaymentStatus
{
    Unpaid,
    Paid
}
