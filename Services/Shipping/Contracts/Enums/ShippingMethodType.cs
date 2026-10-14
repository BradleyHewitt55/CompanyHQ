using System.Text.Json.Serialization;
using CompanyHQ.Postgres.Contracts.Serialization;

namespace CompanyHQ.Shipping.Contracts.Enums;

/// <summary>How a method moves the goods: the company's own delivery, or a courier.</summary>
[JsonConverter(typeof(StrictStringEnumConverter<ShippingMethodType>))]
public enum ShippingMethodType
{
    Default,
    Courier
}
