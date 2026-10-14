using System.Text.Json.Serialization;

using CompanyHQ.Sales.Contracts.Enums;

namespace CompanyHQ.Sales.Contracts.Dtos;

/// <summary>
/// What a product sells for right now. Always computed by the server: the selling price from cost
/// and markup, then any bundle discount and automatic coupons.
/// </summary>
/// <param name="DiscountedPrice">Null when nothing is taken off.</param>
/// <param name="EffectivePrice">What a customer pays for one.</param>
/// <param name="DiscountSource">What took the discount off, for display. Empty when nothing did.</param>
public sealed record ProductPriceDto(
    decimal SellingPrice,
    decimal? DiscountedPrice,
    decimal EffectivePrice,
    string DiscountSource);

/// <summary>A visible product as anyone may see it. <see cref="Price"/> is its cheapest variant.</summary>
[JsonDerivedType(typeof(AdminProductDto))]
public record ClientProductDto(
    Guid Id,
    string Name,
    string Slug,
    string ShortDescription,
    string Description,
    ProductType Type,
    /// <summary>In display order; the first is the product's main image.</summary>
    IReadOnlyList<string> ImageUrls,
    IReadOnlyList<Guid> CategoryIds,
    /// <summary>ISO 4217 code every amount on this product is in.</summary>
    string Currency,
    ProductPriceDto Price,
    IReadOnlyList<ProductOptionDto> Options,
    IReadOnlyList<ClientProductVariantDto> Variants,
    IReadOnlyList<ClientBundleComponentDto> BundleComponents,
    string? Brand=null,IReadOnlyList<string>? Tags=null,decimal AverageRating=0,int ReviewCount=0,int UnitsSold=0,int? BestSellerRank=null,
    IReadOnlyList<ClientRelatedProductDto>? RelatedProducts=null);

/// <summary>The staff view, which adds what the price is built from.</summary>
public sealed record AdminProductDto(
    Guid Id,
    string Name,
    string Slug,
    string ShortDescription,
    string Description,
    ProductType Type,
    IReadOnlyList<string> ImageUrls,
    IReadOnlyList<Guid> CategoryIds,
    string Currency,
    ProductPriceDto Price,
    IReadOnlyList<ProductOptionDto> Options,
    IReadOnlyList<ClientProductVariantDto> Variants,
    IReadOnlyList<ClientBundleComponentDto> BundleComponents,
    bool IsVisible,
    bool EnableRounding,
    decimal RoundingTarget,
    RoundingDirection RoundingDirection,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,string? Brand=null,IReadOnlyList<string>? Tags=null,decimal AverageRating=0,int ReviewCount=0,int UnitsSold=0,int? BestSellerRank=null,IReadOnlyList<Guid>? SupplierIds=null,IReadOnlyList<Guid>? RelatedProductIds=null,
    IReadOnlyList<ClientRelatedProductDto>? RelatedProducts=null)
    : ClientProductDto(Id, Name, Slug, ShortDescription, Description, Type, ImageUrls, CategoryIds, Currency, Price, Options, Variants, BundleComponents,Brand,Tags,AverageRating,ReviewCount,UnitsSold,BestSellerRank,RelatedProducts);

/// <summary>One choice a customer makes, and the values it may take, both in display order.</summary>
[method: JsonConstructor]
public sealed record ProductOptionDto(string Name, IReadOnlyList<string> Values, Guid Id,
    IReadOnlyList<Guid>? DependencyOptionIds=null)
{
    /// <summary>For a new option; saving assigns its stable id.</summary>
    public ProductOptionDto(string Name,IReadOnlyList<string> Values) : this(Name,Values,Guid.Empty) { }
}

/// <summary>One level of public product detail, with no private cost data or recursive relations.</summary>
public sealed record ClientRelatedProductDto(Guid Id,string Name,string Slug,string? ImageUrl,string Currency,ProductPriceDto Price);

/// <summary>One purchasable combination of option values.</summary>
[JsonDerivedType(typeof(AdminProductVariantDto))]
public record ClientProductVariantDto(
    Guid Id,
    string Sku,
    /// <summary>Its option values joined for display, such as <c>Queen / Oak</c>. Empty for a product with no options.</summary>
    string Name,
    /// <summary>Option name to the value this variant has for it.</summary>
    IReadOnlyDictionary<string, string> Options,
    ProductPriceDto Price,
    int? StockQty,ProductSpecificationsDto? Specifications=null);

public sealed record AdminProductVariantDto(
    Guid Id,
    string Sku,
    string Name,
    IReadOnlyDictionary<string, string> Options,
    ProductPriceDto Price,
    int? StockQty,
    decimal Cost,
    decimal ProfitPercentage,
    bool UseCustomRounding,
    decimal RoundingTarget,
    RoundingDirection RoundingDirection,ProductSpecificationsDto? Specifications=null)
    : ClientProductVariantDto(Id, Sku, Name, Options, Price, StockQty,Specifications);

/// <summary>One product a bundle contains.</summary>
[JsonDerivedType(typeof(AdminBundleComponentDto))]
public record ClientBundleComponentDto(Guid ProductId, string ProductName, int Quantity);

public sealed record AdminBundleComponentDto(
    Guid ProductId,
    string ProductName,
    int Quantity,
    decimal DiscountValue,
    DiscountType DiscountType,
    bool SyncOptions,
    Guid? FallbackVariantId,
    /// <summary>Bundle option name to the component option it drives.</summary>
    IReadOnlyDictionary<string, string> OptionMappings)
    : ClientBundleComponentDto(ProductId, ProductName, Quantity);
