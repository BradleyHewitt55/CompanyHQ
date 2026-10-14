using System.Text.Json.Serialization;
using CompanyHQ.Postgres.Contracts.Serialization;

namespace CompanyHQ.Sales.Contracts.Enums;

/// <summary>Where one discount on a line came from.</summary>
[JsonConverter(typeof(StrictStringEnumConverter<SalesDiscountSource>))]
public enum SalesDiscountSource
{
    /// <summary>The bundle's own component discounts.</summary>
    Bundle,

    /// <summary>The offer that came with another product in the same sale.</summary>
    Upsell,

    /// <summary>A coupon the customer entered.</summary>
    Coupon,

    /// <summary>A coupon that applies without being entered.</summary>
    AutomaticCoupon,

    /// <summary>A discount staff gave by hand.</summary>
    Manual
}
