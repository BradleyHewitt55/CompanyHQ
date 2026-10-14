using System.Text.Json.Serialization;
using CompanyHQ.Services.Postgres.Contracts.Serialization;

namespace CompanyHQ.Services.Shipping.Contracts.Enums;

/// <summary>How a method moves the goods: the company's own delivery, or a courier.</summary>
[JsonConverter(typeof(StrictStringEnumConverter<ShippingMethodType>))]
public enum ShippingMethodType
{
    Default,
    Courier
}
