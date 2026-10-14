using CompanyHQ.Services.Persistence.Contracts.Models;
using CompanyHQ.Services.Shipping.Contracts.Enums;

namespace CompanyHQ.Services.Shipping.Contracts.Models;

/// <summary>One way of getting an order to a customer, at one flat cost per origin it leaves from.</summary>
public sealed class ShippingMethod : BaseModel
{
    public required string Name { get; set; }

    public string? Code { get; set; }

    public string Description { get; set; } = string.Empty;

    public decimal Cost { get; set; }

    public int? EstimatedDeliveryDays { get; set; }

    /// <summary>Furthest a destination may be from the origin, in a straight line. Null is unlimited.</summary>
    public int? MaxDistanceKm { get; set; }

    public bool IsActive { get; set; } = true;

    public ShippingMethodType MethodType { get; set; }

    /// <summary>The only products that may ship this way. Empty means every product.</summary>
    public Guid[] ProductIds { get; set; } = [];
}

/// <summary>An area a method delivers to. A method with no active zone delivers anywhere.</summary>
public sealed class DeliveryZone : BaseModel
{
    public Guid ShippingMethodId { get; set; }

    public required string Name { get; set; }

    public string Description { get; set; } = string.Empty;

    public bool IsActive { get; set; } = true;

    public int SortOrder { get; set; }
}

public sealed class DeliveryZonePostalCodeRule : BaseModel
{
    public Guid DeliveryZoneId { get; set; }

    public PostalCodeRuleType RuleType { get; set; }

    public required string Pattern { get; set; }

    public int SortOrder { get; set; }
}

/// <summary>A place goods leave from: a warehouse, a shop, a supplier.</summary>
public sealed class FulfillmentOrigin : BaseModel
{
    public required string Name { get; set; }

    public bool IsActive { get; set; } = true;

    public Guid? SupplierId { get; set; }

    public string? StreetAddress { get; set; }

    public string? ApartmentUnit { get; set; }

    public string? Suburb { get; set; }

    public string? City { get; set; }

    public string? Province { get; set; }

    public string? PostalCode { get; set; }

    public string? Country { get; set; }

    public double? Latitude { get; set; }

    public double? Longitude { get; set; }
}

/// <summary>Which origins a method can leave from.</summary>
public sealed class ShippingMethodFulfillmentOrigin : BaseModel
{
    public Guid ShippingMethodId { get; set; }

    public Guid FulfillmentOriginId { get; set; }
}
