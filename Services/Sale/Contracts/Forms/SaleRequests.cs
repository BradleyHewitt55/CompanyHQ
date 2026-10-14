using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using CompanyHQ.Services.Sales.Contracts.Dtos;
using CompanyHQ.Services.Sales.Contracts.Enums;
namespace CompanyHQ.Services.Sales.Contracts.Forms;

/// <summary>Client cart identity and selections only. No client prices are accepted.</summary>
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record SalesLineRequest(Guid Id, Guid ProductId, Guid? VariantId = null,
    [Range(1, 1000)] int Quantity = 1, IReadOnlyDictionary<string, string>? Options = null,
    Guid? BundleInstanceId = null, Guid? UpsellParentLineId = null);
/// <summary>Browser-owned cart submitted for normalization and server pricing.</summary>
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record PriceSaleRequest([Required, MinLength(1), MaxLength(200)] IReadOnlyList<SalesLineRequest> Lines,
    SalesAddressDto? ShippingAddress = null, [MaxLength(20)] IReadOnlyList<string>? CouponCodes = null);
/// <summary>Places a pending unpaid order. Signed-in ownership is taken from authentication, not the body.</summary>
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record PlaceOrderRequest([Required] PriceSaleRequest Sale,
    [Required, MaxLength(100)] string CustomerFirstName,
    [Required, MaxLength(100)] string CustomerLastName,
    [Required, EmailAddress, MaxLength(255)] string CustomerEmail,
    [MaxLength(50)] string? CustomerPhone = null,
    SalesAddressDto? BillingAddress = null, bool BillingSameAsShipping = true,
    [MaxLength(255)] string? CompanyName = null, [MaxLength(100)] string? VatNumber = null,
    [MaxLength(2000)] string? Notes = null, Guid? PaymentMethodId = null);
/// <summary>Accepts a freshly calculated offer as an explicitly unpaid addition. Amounts are never client input.</summary>
public sealed record AcceptUpsellRequest(Guid OfferId, Guid ParentLineId, Guid RequestId);
/// <summary>Staff status transition. Payment status cannot be changed here.</summary>
public sealed record ChangeOrderStatusRequest(OrderStatus Status);
/// <summary>Promotion validity uses inclusive dates in the configured business timezone.</summary>
public sealed record SaveCouponRequest([Required, MaxLength(100)] string Code, DiscountType DiscountType,
    [Range(0, 100000000)] decimal DiscountValue, bool IsAutomatic = false, bool IsActive = true,
    bool AllowsCombination = false, bool AppliesToShipping = false,
    [Range(0, 100000000)] decimal? MinimumPurchase = null, DateOnly? StartDate = null, DateOnly? EndDate = null,
    IReadOnlyList<Guid>? ProductIds = null, IReadOnlyList<Guid>? CategoryIds = null,
    [Range(-90,90)] double? ShippingOriginLatitude = null, [Range(-180,180)] double? ShippingOriginLongitude = null,
    [Range(0, 50000)] decimal? ShippingMaxDistanceKm = null, [MaxLength(1000)] string Description = "");
/// <summary>Relationship quantity and discounts for an additional product.</summary>
public sealed record SaveUpsellRequest(Guid ParentProductId, Guid ProductId, [Range(1,1000)] int Quantity = 1,
    [Range(0,100000000)] decimal Value = 0, DiscountType ValueType = DiscountType.Percentage, bool SyncVariants = true);
/// <summary>Available payment method; it does not itself execute payment.</summary>
public sealed record SavePaymentMethodRequest([Required, MaxLength(255)] string Name, bool IsActive = true,
    [Range(0,100000000)] decimal? MaxOrderTotal = null, [Range(0,50000)] int? MaxDistanceKm = null,
    [MaxLength(2000)] string? CheckoutNote = null,[Range(0,100000000)] decimal? MinOrderTotal = null);
/// <summary>Company-wide additive upsell percentage.</summary>
public sealed record SaveSalesSettingsRequest([Range(0,100)] decimal UpsellExtraDiscountPercent);
