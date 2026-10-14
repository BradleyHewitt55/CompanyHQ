using System.Text.Json.Serialization;

using CompanyHQ.Services.Shipping.Contracts.Enums;

namespace CompanyHQ.Services.Shipping.Contracts.Dtos;

/// <summary>An active shipping method as anyone may see it.</summary>
[JsonDerivedType(typeof(AdminShippingMethodDto))]
public record ClientShippingMethodDto(
    Guid Id,
    string Name,
    string? Code,
    string Description,
    decimal Cost,
    int? EstimatedDeliveryDays,
    int? MaxDistanceKm,
    ShippingMethodType MethodType);

/// <summary>The staff view, which adds what decides whether the method can serve an order.</summary>
public sealed record AdminShippingMethodDto(
    Guid Id,
    string Name,
    string? Code,
    string Description,
    decimal Cost,
    int? EstimatedDeliveryDays,
    int? MaxDistanceKm,
    ShippingMethodType MethodType,
    bool IsActive,
    /// <summary>Empty means every product; otherwise only these may ship this way.</summary>
    IReadOnlyList<Guid> ProductIds,
    IReadOnlyList<Guid> FulfillmentOriginIds,
    IReadOnlyList<AdminDeliveryZoneDto> DeliveryZones,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt)
    : ClientShippingMethodDto(Id, Name, Code, Description, Cost, EstimatedDeliveryDays, MaxDistanceKm, MethodType);

/// <summary>One area a method delivers to, as a set of postal code rules.</summary>
public sealed record AdminDeliveryZoneDto(
    Guid Id,
    string Name,
    string Description,
    bool IsActive,
    IReadOnlyList<AdminPostalCodeRuleDto> Rules);

public sealed record AdminPostalCodeRuleDto(PostalCodeRuleType RuleType, string Pattern, int SortOrder);
