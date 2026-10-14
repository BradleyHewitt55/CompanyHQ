using System.ComponentModel.DataAnnotations;

using CompanyHQ.Shipping.Contracts.Enums;

namespace CompanyHQ.Shipping.Contracts.Forms;

/// <summary>
/// Creates or replaces a shipping method with everything that decides where it delivers. Zones,
/// the product allow-list and the origin links are replaced as a whole.
/// </summary>
public sealed record SaveShippingMethodRequest(
    [Required, MaxLength(255), RegularExpression(@".*\S.*")] string Name,
    [Range(0, 1_000_000_000)] decimal Cost,
    [MaxLength(100)] string? Code = null,
    [MaxLength(1_000)] string? Description = null,
    [Range(0, 3_650)] int? EstimatedDeliveryDays = null,
    [Range(1, 50_000)] int? MaxDistanceKm = null,
    bool IsActive = true,
    [EnumDataType(typeof(ShippingMethodType))] ShippingMethodType MethodType = ShippingMethodType.Default,
    [MaxLength(5_000)] IReadOnlyList<Guid>? ProductIds = null,
    [MaxLength(200)] IReadOnlyList<Guid>? FulfillmentOriginIds = null,
    [MaxLength(200)] IReadOnlyList<SaveDeliveryZoneRequest>? DeliveryZones = null);

public sealed record SaveDeliveryZoneRequest(
    [Required, MaxLength(255), RegularExpression(@".*\S.*")] string Name,
    [MaxLength(1_000)] string? Description = null,
    bool IsActive = true,
    [MaxLength(2_000)] IReadOnlyList<SavePostalCodeRuleRequest>? Rules = null);

public sealed record SavePostalCodeRuleRequest(
    [EnumDataType(typeof(PostalCodeRuleType))] PostalCodeRuleType RuleType,
    [Required, MaxLength(64), RegularExpression(@".*\S.*")] string Pattern,
    int SortOrder = 0);
