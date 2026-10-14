using System.Text.Json.Serialization;
using CompanyHQ.Postgres.Contracts.Serialization;

namespace CompanyHQ.Sales.Contracts.Enums;

/// <summary>How a discount value is read. <see cref="FreeShipping"/> only means something on a shipping coupon.</summary>
[JsonConverter(typeof(StrictStringEnumConverter<DiscountType>))]
public enum DiscountType
{
    Percentage,
    Fixed,
    FreeShipping
}
