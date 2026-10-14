namespace CompanyHQ.Services.Sales.Storage;

// Frozen persisted schema, independent of Contracts. Add a version to evolve storage.
internal sealed record CatalogDefinitionV1(string Name,
    string Slug,
    IReadOnlyList<CatalogVariantV1> Variants,
    ProductTypeV1 Type = ProductTypeV1.Standard,
    bool IsVisible = true,
    string ShortDescription = "",
    string Description = "",
    string Currency = "",
    IReadOnlyList<string>? ImageUrls = null,
    IReadOnlyList<Guid>? CategoryIds = null,
    IReadOnlyList<Guid>? SupplierIds = null,
    IReadOnlyList<CatalogOptionV1>? Options = null,
    IReadOnlyList<BundleComponentV1>? BundleComponents = null,
    bool EnableRounding = false,
    decimal RoundingTarget = 99,
    RoundingDirectionV1 RoundingDirection = RoundingDirectionV1.RoundUp,
    string? Brand = null,IReadOnlyList<string>? Tags = null,IReadOnlyList<Guid>? RelatedProductIds = null);

internal sealed record CatalogVariantV1(Guid Id,
    string Sku,
    decimal Cost,
    decimal ProfitPercentage = 0,
    IReadOnlyDictionary<string, string>? Options = null,
    int? StockQty = null,
    bool UseCustomRounding = false,
    decimal RoundingTarget = 99,
    RoundingDirectionV1 RoundingDirection = RoundingDirectionV1.RoundUp,SpecificationV1? Specifications = null);

internal sealed record BundleComponentV1(Guid ProductId, int Quantity = 1,
    decimal DiscountValue = 0, DiscountTypeV1 DiscountType = DiscountTypeV1.Percentage,
    bool SyncOptions = true, Guid? FallbackVariantId = null, IReadOnlyDictionary<string, string>? OptionMappings = null);

internal sealed record CatalogOptionV1(string Name, IReadOnlyList<string> Values,Guid Id=default,
    IReadOnlyList<Guid>? DependencyOptionIds=null);

internal sealed record SpecificationV1(decimal? Width=null,decimal? Height=null,decimal? Length=null,string DimensionUnit="cm",
    decimal? Weight=null,string WeightUnit="kg",IReadOnlyDictionary<string,string>? Attributes=null,IReadOnlyDictionary<string,string>? Policies=null);

internal sealed record AddressV1(string? StreetAddress = null,
    string? ApartmentUnit = null,
    string? Suburb = null,
    string? City = null,
    string? Province = null,
    string? PostalCode = null,
    string? Country = null,
    double? Latitude = null,
    double? Longitude = null);

internal sealed record CouponDefinitionV1(string Code, DiscountTypeV1 DiscountType,
    decimal DiscountValue, bool IsAutomatic = false, bool IsActive = true,
    bool AllowsCombination = false, bool AppliesToShipping = false,
    decimal? MinimumPurchase = null, DateOnly? StartDate = null, DateOnly? EndDate = null,
    IReadOnlyList<Guid>? ProductIds = null, IReadOnlyList<Guid>? CategoryIds = null,
    double? ShippingOriginLatitude = null, double? ShippingOriginLongitude = null,
    decimal? ShippingMaxDistanceKm = null, string Description = "");

internal sealed record UpsellDefinitionV1(Guid ParentProductId, Guid ProductId, int Quantity = 1,
    decimal Value = 0, DiscountTypeV1 ValueType = DiscountTypeV1.Percentage, bool SyncVariants = true);

internal sealed record PaymentDefinitionV1(string Name, bool IsActive = true,
    decimal? MaxOrderTotal = null, int? MaxDistanceKm = null,
    string? CheckoutNote = null,decimal? MinOrderTotal = null);

internal sealed record CustomerOrderV1(CustomerSaleV1 Sale,
    string CustomerFirstName,
    string CustomerLastName,
    string CustomerEmail,
    string? CustomerPhone = null,
    AddressV1? BillingAddress = null, bool BillingSameAsShipping = true,
    string? CompanyName = null, string? VatNumber = null,
    string? Notes = null, Guid? PaymentMethodId = null);

internal sealed record CustomerSaleV1(IReadOnlyList<CartLineV1> Lines,
    AddressV1? ShippingAddress = null, IReadOnlyList<string>? CouponCodes = null);

internal sealed record CartLineV1(Guid Id, Guid ProductId, Guid? VariantId = null,
    int Quantity = 1, IReadOnlyDictionary<string, string>? Options = null,
    Guid? BundleInstanceId = null, Guid? UpsellParentLineId = null);

internal sealed record CartV1(IReadOnlyList<CartLineV1> Lines, IReadOnlyList<Guid> RemovedLineIds);

internal sealed record PricedSaleV1(string Currency,
    IReadOnlyList<CustomerLineV1> Lines,
    IReadOnlyList<CouponSnapshotV1> Coupons,
    int ItemCount,
    decimal Subtotal,
    
    decimal DiscountAmount,
    decimal OrderCouponDiscount,
    decimal ManualDiscountAmount,
    decimal ShippingCost,
    decimal ShippingDiscount,
    decimal TaxAmount,
    decimal TotalAmount,
    ShippingSummaryV1? Shipping,
    
    
    
    
    IReadOnlyList<Guid> RemovedLineIds);

internal sealed record CustomerLineV1(Guid Id,
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
    IReadOnlyList<LineDiscountV1> Discounts);

internal sealed record CostedLineV1(Guid Id,
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
    IReadOnlyList<LineDiscountV1> Discounts,
    decimal CostPriceUsed,
    decimal CostTotalUsed,
    decimal ProfitAmount,
    decimal ProfitMargin);

internal sealed record LineDiscountV1(SalesDiscountSourceV1 SourceType,
    string Label,
    string? CouponCode,
    decimal DiscountAmount,
    DiscountTypeV1? AppliedValueType,
    decimal AppliedValue);

internal sealed record CouponSnapshotV1(Guid? CouponId,
    string CouponCode,
    DiscountTypeV1 DiscountType,
    decimal DiscountValue,
    bool AllowsCombination,
    bool AppliesToShipping,
    decimal AppliedDiscountAmount);

internal sealed record ShippingSummaryV1(bool IsAvailable,
    string? Message,
    string? MethodName,
    int? EstimatedDeliveryDays);

internal sealed record ShippingQuoteV1(bool IsAvailable, string? Message, Guid? MethodId, string? MethodName,
    string? MethodKind, int? EstimatedDeliveryDays, decimal BaseCost, decimal Discount, decimal TotalCost,
    IReadOnlyList<string> CouponCodes, IReadOnlyList<RouteV1> Routes, double? DestinationLatitude, double? DestinationLongitude);

internal sealed record RouteV1(Guid MethodId, string MethodName, Guid OriginId, string OriginName, decimal Cost,
    double? DistanceKm, double? Latitude, double? Longitude, IReadOnlyList<Guid> ProductIds);

internal sealed record UpsellReceiptV1(Guid RequestId, Guid LineId, decimal AdditionalAmount, decimal OrderTotal, bool IsPaid);

internal sealed record PreparedDocumentV1(CustomerOrderV1 Request,BasketV1 Priced,string? PaymentMethodName,StaffPricingV1? Adjustments=null);

internal sealed record BasketV1(CartV1 Cart, PricedSaleV1 Price, IReadOnlyList<CostedLineV1> AdminLines, ShippingQuoteV1? Shipping);

internal sealed record StaffPricingV1(IReadOnlyList<StaffLineDiscountV1>? LineDiscounts=null,
    DiscountTypeV1? ManualDiscountType=null,decimal ManualDiscountValue=0,
    decimal? NegotiatedShippingCost=null,decimal TaxAmount=0);

internal sealed record StaffLineDiscountV1(Guid LineId,DiscountTypeV1 Type,decimal Value);
internal enum ProductTypeV1 { Standard=0, Variant=1, Bundle=2 }
internal enum DiscountTypeV1 { Percentage=0, Fixed=1, FreeShipping=2 }
internal enum RoundingDirectionV1 { RoundUp=0, RoundDown=1 }
internal enum SalesDiscountSourceV1 { Bundle=0, Upsell=1, Coupon=2, AutomaticCoupon=3, Manual=4 }
