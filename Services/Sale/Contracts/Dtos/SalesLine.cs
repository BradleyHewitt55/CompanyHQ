using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

using CompanyHQ.Sales.Contracts.Enums;

namespace CompanyHQ.Sales.Contracts.Dtos;

/// <summary>
/// A street address on an order or quote. It is both what a caller sends and what they read back;
/// coordinates are optional and let shipping measure distance without geocoding.
/// </summary>
public sealed record SalesAddressDto(
    [MaxLength(255)] string? StreetAddress = null,
    [MaxLength(100)] string? ApartmentUnit = null,
    [MaxLength(150)] string? Suburb = null,
    [MaxLength(150)] string? City = null,
    [MaxLength(150)] string? Province = null,
    [MaxLength(32)] string? PostalCode = null,
    [MaxLength(100)] string? Country = null,
    [Range(-90, 90)] double? Latitude = null,
    [Range(-180, 180)] double? Longitude = null);

/// <summary>One priced line, as the customer sees it. Every amount is the server's.</summary>
[JsonDerivedType(typeof(AdminSalesLineDto))]
public record ClientSalesLineDto(
    Guid Id,
    Guid ProductId,
    Guid? VariantId,
    /// <summary>Set when this line is an upsell: the product in the same sale that offered it.</summary>
    Guid? UpsellParentProductId,
    string ProductName,
    string? VariantName,
    string? Sku,
    string? ImageUrl,
    int Quantity,
    decimal OriginalUnitPrice,
    decimal OriginalTotalPrice,
    decimal FinalUnitPrice,
    decimal FinalTotalPrice,
    decimal LineDiscountAmount,
    string? DiscountSource,
    IReadOnlyList<SalesLineDiscountDto> Discounts);

/// <summary>The staff view, which adds what the line cost and earned.</summary>
public sealed record AdminSalesLineDto(
    Guid Id,
    Guid ProductId,
    Guid? VariantId,
    Guid? UpsellParentProductId,
    string ProductName,
    string? VariantName,
    string? Sku,
    string? ImageUrl,
    int Quantity,
    decimal OriginalUnitPrice,
    decimal OriginalTotalPrice,
    decimal FinalUnitPrice,
    decimal FinalTotalPrice,
    decimal LineDiscountAmount,
    string? DiscountSource,
    IReadOnlyList<SalesLineDiscountDto> Discounts,
    decimal CostPriceUsed,
    decimal CostTotalUsed,
    decimal ProfitAmount,
    decimal ProfitMargin)
    : ClientSalesLineDto(Id, ProductId, VariantId, UpsellParentProductId, ProductName, VariantName, Sku, ImageUrl, Quantity,
        OriginalUnitPrice, OriginalTotalPrice, FinalUnitPrice, FinalTotalPrice, LineDiscountAmount, DiscountSource, Discounts);

/// <summary>One thing that took money off a line, in the order it was applied.</summary>
public sealed record SalesLineDiscountDto(
    SalesDiscountSource SourceType,
    string Label,
    string? CouponCode,
    decimal DiscountAmount,
    DiscountType? AppliedValueType,
    decimal AppliedValue);

/// <summary>A coupon as it stood when it was applied, and what it took off.</summary>
public sealed record SalesAppliedCouponDto(
    Guid? CouponId,
    string CouponCode,
    DiscountType DiscountType,
    decimal DiscountValue,
    bool AllowsCombination,
    bool AppliesToShipping,
    decimal AppliedDiscountAmount);

/// <summary>
/// A basket priced by the server: what checkout shows and exactly what an order placed with the same
/// input will charge. Self-service, so one shape, with staff lines carrying cost when staff ask.
/// </summary>
public sealed record PricedSaleDto(
    string Currency,
    IReadOnlyList<ClientSalesLineDto> Lines,
    IReadOnlyList<SalesAppliedCouponDto> Coupons,
    int ItemCount,
    decimal Subtotal,
    /// <summary>Everything taken off: line discounts, order coupons, the manual discount and shipping discount.</summary>
    decimal DiscountAmount,
    decimal OrderCouponDiscount,
    decimal ManualDiscountAmount,
    decimal ShippingCost,
    decimal ShippingDiscount,
    decimal TaxAmount,
    decimal TotalAmount,
    SalesShippingSummaryDto? Shipping,
    /// <summary>
    /// Lines the server dropped: an upsell whose parent product is no longer in the basket. A cart
    /// should forget these ids.
    /// </summary>
    IReadOnlyList<Guid> RemovedLineIds);

/// <summary>How the basket ships, when a shipping address was given and the host can quote.</summary>
public sealed record SalesShippingSummaryDto(
    bool IsAvailable,
    string? Message,
    string? MethodName,
    int? EstimatedDeliveryDays);
