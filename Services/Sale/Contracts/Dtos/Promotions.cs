using System.Text.Json.Serialization;

using CompanyHQ.Sales.Contracts.Enums;

namespace CompanyHQ.Sales.Contracts.Dtos;

/// <summary>A coupon. Staff only: listing codes to customers would hand out every discount.</summary>
public sealed record AdminCouponDto(
    Guid Id,
    string Code,
    string Description,
    DiscountType DiscountType,
    decimal DiscountValue,
    bool IsAutomatic,
    decimal? MinimumPurchase,
    DateOnly? StartDate,
    DateOnly? EndDate,
    bool IsActive,
    bool AllowsCombination,
    bool AppliesToShipping,
    double? ShippingOriginLatitude,
    double? ShippingOriginLongitude,
    decimal? ShippingMaxDistanceKm,
    IReadOnlyList<Guid> ProductIds,
    IReadOnlyList<Guid> CategoryIds,
    /// <summary>Whether it would apply today: active and inside its date window.</summary>
    bool IsCurrentlyValid,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);

/// <summary>The rule that offers one product alongside another. Staff only.</summary>
public sealed record AdminUpsellOfferDto(
    Guid Id,
    Guid ParentProductId,
    string? ParentProductName,
    Guid ProductId,
    string? ProductName,
    int Quantity,
    decimal Value,
    DiscountType ValueType,
    bool SyncVariants,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);

/// <summary>
/// An upsell priced for one customer: which variant goes with what they chose, and what it costs.
/// Self-service, so one shape.
/// </summary>
public sealed record UpsellOfferDto(
    Guid UpsellOfferId,
    Guid ParentProductId,
    Guid ProductId,
    Guid VariantId,
    string ProductName,
    string? VariantName,
    string? ImageUrl,
    /// <summary>How many the offer covers. A line may take fewer, never more.</summary>
    int Quantity,
    string Currency,
    decimal BaseUnitPrice,
    /// <summary>The unit price after automatic coupons and bundle discount, before the offer.</summary>
    decimal CouponAdjustedUnitPrice,
    decimal FinalUnitPrice,
    decimal OriginalLineTotal,
    decimal FinalLineTotal,
    decimal EffectiveDiscountPercent,
    string DiscountSource);

/// <summary>A way to pay that checkout may offer.</summary>
[JsonDerivedType(typeof(AdminPaymentMethodDto))]
public record ClientPaymentMethodDto(
    Guid Id,
    string Name,
    /// <summary>Orders above this total may not use it. Null is unlimited.</summary>
    decimal? MaxOrderTotal,
    /// <summary>Deliveries further than this may not use it. Null is unlimited.</summary>
    int? MaxDistanceKm,
    string? CheckoutNote,decimal? MinOrderTotal=null);

public sealed record AdminPaymentMethodDto(
    Guid Id,
    string Name,
    decimal? MaxOrderTotal,
    int? MaxDistanceKm,
    string? CheckoutNote,
    bool IsActive,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,decimal? MinOrderTotal=null)
    : ClientPaymentMethodDto(Id, Name, MaxOrderTotal, MaxDistanceKm, CheckoutNote,MinOrderTotal);

/// <summary>Company-wide selling settings. Staff only.</summary>
public sealed record AdminSalesSettingsDto(decimal UpsellExtraDiscountPercent, DateTimeOffset? UpdatedAt);
