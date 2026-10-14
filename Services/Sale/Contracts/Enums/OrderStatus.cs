using System.Text.Json.Serialization;
using CompanyHQ.Postgres.Contracts.Serialization;

namespace CompanyHQ.Sales.Contracts.Enums;

[JsonConverter(typeof(StrictStringEnumConverter<OrderStatus>))]
public enum OrderStatus
{
    Pending,
    Processing,
    Shipped,
    Delivered,
    Cancelled
}
