using System.ComponentModel.DataAnnotations;
using CompanyHQ.Services.Sales.Contracts.Dtos;
using CompanyHQ.Services.Sales.Contracts.Enums;
namespace CompanyHQ.Services.Sales.Contracts.Forms;

/// <summary>Complete catalog definition. Variant ids are stable across updates.</summary>
public sealed record SaveProductRequest(
    [Required, MaxLength(255)] string Name,
    [Required, MaxLength(255)] string Slug,
    [Required] IReadOnlyList<SaveVariantRequest> Variants,
    ProductType Type = ProductType.Standard,
    bool IsVisible = true,
    [MaxLength(1000)] string ShortDescription = "",
    [MaxLength(20000)] string Description = "",
    [MaxLength(3), MinLength(3)] string Currency = "",
    IReadOnlyList<string>? ImageUrls = null,
    IReadOnlyList<Guid>? CategoryIds = null,
    IReadOnlyList<Guid>? SupplierIds = null,
    IReadOnlyList<ProductOptionDto>? Options = null,
    IReadOnlyList<SaveBundleComponentRequest>? BundleComponents = null,
    bool EnableRounding = false,
    [Range(0, 999999)] decimal RoundingTarget = 99,
    RoundingDirection RoundingDirection = RoundingDirection.RoundUp,
    [MaxLength(255)] string? Brand = null,IReadOnlyList<string>? Tags = null,IReadOnlyList<Guid>? RelatedProductIds = null);

/// <summary>Cost plus markup and a purchasable combination of options.</summary>
public sealed record SaveVariantRequest(
    Guid Id,
    [Required, MaxLength(100)] string Sku,
    [Range(0, 100000000)] decimal Cost,
    [Range(0, 10000)] decimal ProfitPercentage = 0,
    IReadOnlyDictionary<string, string>? Options = null,
    [Range(0, int.MaxValue)] int? StockQty = null,
    bool UseCustomRounding = false,
    [Range(0, 999999)] decimal RoundingTarget = 99,
    RoundingDirection RoundingDirection = RoundingDirection.RoundUp,ProductSpecificationsDto? Specifications = null);

/// <summary>Component selection and discount, applied per unit before quantity.</summary>
public sealed record SaveBundleComponentRequest(Guid ProductId, [Range(1, 1000)] int Quantity = 1,
    [Range(0, 100000000)] decimal DiscountValue = 0, DiscountType DiscountType = DiscountType.Percentage,
    bool SyncOptions = true, Guid? FallbackVariantId = null, IReadOnlyDictionary<string, string>? OptionMappings = null);

/// <summary>Filters all selected options against the same variant.</summary>
public sealed record ProductFilterRequest(Guid? CategoryId = null, Guid? SupplierId = null, ProductType? Type = null,
    Dictionary<string, string>? Options = null, bool? Sale=null,decimal? MinPrice=null,decimal? MaxPrice=null,int? MinRating=null,
    IReadOnlyList<Guid>? CategoryIds=null,string Sort="name-asc");

/// <summary>A public catalog category.</summary>
public sealed record SaveCategoryRequest([Required, MaxLength(255)] string Name, [Required, MaxLength(255)] string Slug,Guid? ParentId=null,int Position=0,[MaxLength(100)] string? Sku=null,[MaxLength(2000)] string? ImageUrl=null);
/// <summary>Supplier origin is server-managed catalog geography, never customer identity.</summary>
public sealed record SaveSupplierRequest([Required, MaxLength(255)] string Name, SalesAddressDto? Origin = null, bool IsActive = true);

/// <summary>Staff-managed review, including imported legitimate reviews.</summary>
public sealed record SaveProductReviewRequest([Required,MaxLength(255)] string Title,[MaxLength(2000)] string Comment,
    [Range(1,5)] int Rating,[Required,MaxLength(255)] string AuthorName);
