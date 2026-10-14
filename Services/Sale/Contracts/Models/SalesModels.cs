using CompanyHQ.Services.Postgres.Contracts.Models;
using CompanyHQ.Services.Sales.Contracts.Enums;
namespace CompanyHQ.Services.Sales.Contracts.Models;
public sealed class Product : BaseModel
{
    public string Name { get; set; } = "";
    public string Slug { get; set; } = "";
    public string ShortDescription { get; set; } = "";
    public string Description { get; set; } = "";
    public ProductType Type { get; set; }
    public bool IsVisible { get; set; }
    public Guid[] CategoryIds { get; set; } = [];
    public Guid[] SupplierIds { get; set; } = [];
    public string DefinitionJson { get; set; } = "";
    public decimal EffectivePrice { get; set; }
    public long PriceGeneration { get; set; }
    public DateOnly PriceDate { get; set; }
    public string PricePolicyKey { get; set; } = "";
    public bool IsOnSale { get; set; }
    public decimal AverageRating { get; set; }
    public int ReviewCount { get; set; }
    public int RatingTotal { get; set; }
    public int UnitsSold { get; set; }
}
public sealed class CatalogVariant : BaseModel
{
    public decimal EffectivePrice { get; set; }
    public bool IsOnSale { get; set; }
    public Guid ProductId { get; set; }
    public string Sku { get; set; } = "";
}
public sealed class VariantSelection : BaseModel
{
    public Guid VariantId { get; set; }
    public string Name { get; set; } = "";
    public string Value { get; set; } = "";
}
public sealed class BundleLink : BaseModel
{
    public Guid ProductId { get; set; }
    public Guid ComponentProductId { get; set; }
}
public sealed class Category : BaseModel
{
    public Guid? ParentId { get; set; }
    public int Position { get; set; }
    public string? Sku { get; set; }
    public string? ImageUrl { get; set; }
    public string Name { get; set; } = "";
    public string Slug { get; set; } = "";
}
public sealed class Supplier : BaseModel
{
    public string Name { get; set; } = "";
    public bool IsActive { get; set; }
    public string? OriginJson { get; set; }
}
public sealed class Coupon : BaseModel
{
    public string Code { get; set; } = "";
    public bool IsAutomatic { get; set; }
    public bool IsActive { get; set; }
    public bool AppliesToShipping { get; set; }
    public string DefinitionJson { get; set; } = "";
}
public sealed class UpsellOffer : BaseModel
{
    public Guid ParentProductId { get; set; }
    public Guid ProductId { get; set; }
    public string DefinitionJson { get; set; } = "";
}
public sealed class SalesSettings : BaseModel
{
    public decimal UpsellExtraDiscountPercent { get; set; }
}
public sealed class PaymentMethod : BaseModel
{
    public string Name { get; set; } = "";
    public bool IsActive { get; set; }
    public string DefinitionJson { get; set; } = "";
}
public sealed class SalesCounter : BaseModel
{
    public int Number { get; set; }
    public int InvoiceNumber { get; set; }
}
public sealed class SalesOrder : BaseModel
{
    public string CustomerFirstName { get; set; } = "";
    public string CustomerLastName { get; set; } = "";
    public string CustomerEmail { get; set; } = "";
    public int Number { get; set; }
    public int InvoiceNumber { get; set; }
    public string InvoiceReference { get; set; } = "";
    public int RevisionNumber { get; set; }
    public DiscountType? ManualDiscountType { get; set; }
    public decimal ManualDiscountValue { get; set; }
    public Guid? SourceQuoteId { get; set; }
    public Guid? UserId { get; set; }
    public string? GuestTokenHash { get; set; }
    public DateTimeOffset? GuestTokenExpiresAt { get; set; }
    public OrderStatus Status { get; set; }
    public PaymentStatus PaymentStatus { get; set; }
    public DateTimeOffset? FinalizedAt { get; set; }
    public DateTimeOffset? ShippedAt { get; set; }
    public DateTimeOffset? DeliveredAt { get; set; }
    public string RequestJson { get; set; } = "";
    public string SnapshotJson { get; set; } = "";
    public string? ShippingJson { get; set; }
    public string? PaymentMethodName { get; set; }
    public decimal TotalAmount { get; set; }
    public string Currency { get; set; } = "";
    public int ItemCount { get; set; }
}
public sealed class OrderUpsellAcceptance : BaseModel
{
    public int RevisionNumber { get; set; }
    public Guid OrderId { get; set; }
    public Guid RequestId { get; set; }
    public Guid OfferId { get; set; }
    public string ResultJson { get; set; } = "";
    public string LineJson { get; set; } = "";
    public string PricedLineJson { get; set; } = "";
    public string CouponsJson { get; set; } = "[]";
    public string? ShippingJson { get; set; }
}

public sealed class SalesQuote : BaseModel
{
    public Guid? DeliveryRequestId { get; set; }
    public int Number { get; set; }
    public string QuoteReference { get; set; } = "";
    public Guid? UserId { get; set; }
    public DateTimeOffset ValidUntil { get; set; }
    public QuoteStatus Status { get; set; }
    public DateTimeOffset? AcceptedAt { get; set; }
    public Guid? ConvertedToOrderId { get; set; }
    public int? ConvertedOrderNumber { get; set; }
    public string PreparedJson { get; set; } = "";
    public string CustomerFirstName { get; set; } = "";
    public string CustomerLastName { get; set; } = "";
    public string CustomerEmail { get; set; } = "";
    public string Currency { get; set; } = "";
    public decimal TotalAmount { get; set; }
    public int ItemCount { get; set; }
}

public sealed class SalesOrderRevision : BaseModel
{
    public Guid OrderId { get; set; }
    public int Number { get; set; }
    public Guid ActingUserId { get; set; }
    public string PreparedJson { get; set; } = "";
}

public sealed class InvoiceDeliveryRequest : BaseModel
{
    public Guid OrderId { get; set; }
    public Guid RequestId { get; set; }
    public int RevisionNumber { get; set; }
    public string DocumentJson { get; set; } = "";
    public DateTimeOffset? QueuedAt { get; set; }
    public DateTimeOffset? AcceptedAt { get; set; }
}

public sealed class ProductReview : BaseModel
{
    public Guid ProductId { get; set; }
    public string Title { get; set; } = "";
    public string Comment { get; set; } = "";
    public int Rating { get; set; }
    public string AuthorName { get; set; } = "";
}
public sealed class CatalogPriceProjection : BaseModel
{
    public long Generation { get; set; } = 1;
    public Guid? LastProductId { get; set; }
    public bool Dirty { get; set; } = true;
    public DateOnly PriceDate { get; set; }
    public string PolicyKey { get; set; } = "";
}
