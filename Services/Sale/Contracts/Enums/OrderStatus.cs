using System.Text.Json.Serialization;
using CompanyHQ.Services.Postgres.Contracts.Serialization;

namespace CompanyHQ.Services.Sales.Contracts.Enums;

[JsonConverter(typeof(StrictStringEnumConverter<OrderStatus>))]
public enum OrderStatus
{
    Pending,
    Processing,
    Shipped,
    Delivered,
    Cancelled
}
