using CompanyHQ.Services.Shipping.Contracts.Enums;

namespace CompanyHQ.Services.Shipping.Contracts.Dtos;

/// <summary>
/// The cheapest way to ship a basket to one address, or why it cannot be shipped. Self-service: the
/// same shape for every caller.
/// </summary>
public sealed record ShippingQuoteDto(
    bool IsAvailable,
    string? Message,
    /// <summary>The first leg's method, for a caller that shows a single line.</summary>
    Guid? ShippingMethodId,
    string? ShippingMethodName,
    ShippingMethodType? ShippingMethodType,
    int? EstimatedDeliveryDays,
    decimal BaseShippingCost,
    decimal ShippingDiscount,
    decimal TotalShippingCost,
    IReadOnlyList<string> AppliedShippingCouponCodes,
    IReadOnlyList<ShippingRouteLegDto> RouteLegs,
    IReadOnlyList<ShippingQuoteFailureDto> Failures,
    /// <summary>Where the address resolved to, when it did.</summary>
    double? DestinationLatitude,
    double? DestinationLongitude);

/// <summary>One method leaving one origin, charged once however many products ride on it.</summary>
public sealed record ShippingRouteLegDto(
    Guid ShippingMethodId,
    string ShippingMethodName,
    Guid OriginId,
    string OriginName,
    decimal Cost,
    /// <summary>Straight-line distance, when both ends have coordinates.</summary>
    double? DistanceKm,
    double? OriginLatitude,
    double? OriginLongitude,
    IReadOnlyList<Guid> ProductIds);

/// <summary>Why one product could not ride one method. Codes are stable; messages are for people.</summary>
public sealed record ShippingQuoteFailureDto(string Code, string Message, Guid? ProductId, Guid? ShippingMethodId);

/// <summary>The stable <see cref="ShippingQuoteFailureDto.Code"/> values.</summary>
public static class ShippingQuoteFailureCodes
{
    public const string ComplexityLimit = "complexity_limit";
    public const string ProductNotFound = "product_not_found";
    public const string ProductWithoutOrigin = "product_without_origin";
    public const string OutOfZone = "out_of_zone";
    public const string OutOfRange = "out_of_range";
    public const string DestinationUnresolved = "destination_unresolved";
    public const string OriginUnresolved = "origin_unresolved";
    public const string NoShippingMethod = "no_shipping_method";
}
